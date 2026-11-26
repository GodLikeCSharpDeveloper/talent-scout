using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Api.Features.Conversations.Requests;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class ProcessUnipileWebhookHandler(
    ILinkedInWebhookRepository inbox,
    ILinkedInConversationRepository conversations,
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    ReceiveReplyHandler receive,
    ILinkedInConversations linkedInConversations)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await inbox.TryClaimAsync(id, ct);

        if (item is null)
            return;

        try
        {
            if (item.EventType == "new_relation")
            {
                var relation = await conversations.FindByRecipientAsync(item.AccountId, item.SenderId, ct);
                if (relation is null)
                {
                    await inbox.DeferAsync(id, "ConversationNotMapped", ct);

                    return;
                }

                relation.ConnectionConfirmedAt = item.OccurredAt;
                relation.ConnectionState = "Connected";
                var owner = await enrollments.FindAsync(relation.EnrollmentId, ct);
                if (owner is not null)
                {
                    owner.NextContactAt = null;
                    audit.Add(owner.CampaignId, owner.Id, "LinkedInConnectionAccepted");
                }

                item.ProcessedAt = DateTimeOffset.UtcNow;
                item.LeaseUntil = null;
                item.LastError = null;
                await unitOfWork.SaveChangesAsync(ct);

                return;
            }

            var conversation = await conversations.FindByChatAsync(item.AccountId, item.ChatId, ct)
                               ?? await conversations.FindByRecipientAsync(item.AccountId, item.SenderId, ct);
            if (conversation is null || conversation.RecipientId != item.SenderId)
            {
                await inbox.DeferAsync(id, "ConversationNotMapped", ct);

                return;
            }

            if (conversation.ChatId is not null && conversation.ChatId != item.ChatId)
            {
                await inbox.DeferAsync(id, "DifferentChatRequiresBinding", ct);

                return;
            }

            if (conversation.ChatId is null)
            {
                var chat = await linkedInConversations.GetChatAsync(item.ChatId, ct);
                if (chat.RecipientId != item.SenderId)
                {
                    item.ProcessedAt = DateTimeOffset.UtcNow;
                    item.LeaseUntil = null;
                    item.LastError = "IgnoredGroupOrUnrelatedChat";
                    await unitOfWork.SaveChangesAsync(ct);

                    return;
                }

                conversation.ProviderChatId = chat.ProviderChatId;
            }

            var enrollment = await enrollments.FindAsync(conversation.EnrollmentId, ct);
            if (enrollment is not null && !enrollment.HasSentInitialMessage &&
                conversation.ConnectionRequestDelivery == DeliveryStatus.Sent && !string.IsNullOrWhiteSpace(conversation.ConnectionNote))
            {
                // A candidate can reply to the invitation note before the full introduction is sent.
                // Record that note as the first contact so the reply is analyzed instead of sending a duplicate introduction.
                var initial = await messages.FindOutgoingAsync(enrollment.Id, MessageKind.Initial, ct);
                if (initial is null || initial.Delivery == DeliveryStatus.Pending)
                {
                    if (initial is null)
                    {
                        initial = new Message { EnrollmentId = enrollment.Id, Kind = MessageKind.Initial };
                        messages.Add(initial);
                    }

                    initial.Content = conversation.ConnectionNote;
                    initial.Delivery = DeliveryStatus.Sent;
                    initial.SentAt = conversation.ConnectionRequestedAt;
                    enrollment.HasSentInitialMessage = true;
                    if (enrollment.Status == CandidateStatus.Screened)
                        enrollment.Status = CandidateStatus.AwaitingReply;
                    conversation.ConnectionConfirmedAt ??= item.OccurredAt;
                    audit.Add(enrollment.CampaignId, enrollment.Id, "ConnectionNoteReplyReceived");
                    await unitOfWork.SaveChangesAsync(ct);
                }
            }

            if (enrollment is null || !enrollment.HasSentInitialMessage)
            {
                await inbox.DeferAsync(id, "InitialDeliveryNotConfirmed", ct);

                return;
            }

            conversation.ChatId = item.ChatId;
            conversation.ProviderChatId = item.ProviderChatId ?? conversation.ProviderChatId;
            if (item.HasAttachments)
            {
                if (await messages.FindByExternalIdAsync(item.ExternalMessageId, ct) is null)
                {
                    messages.Add(
                        new Message
                        {
                            EnrollmentId = enrollment.Id, ExternalId = item.ExternalMessageId,
                            Content = string.IsNullOrEmpty(item.Content) ? "[Attachment received; recruiter review required.]" : item.Content,
                            Kind = MessageKind.Reply, Delivery = DeliveryStatus.Received, CreatedAt = item.OccurredAt,
                            Analyzed = true, Interest = Interest.Ambiguous, Analysis = "Review the attachment in LinkedIn."
                        });
                    if (enrollment.Status != CandidateStatus.InterviewBooked)
                        enrollment.Status = CandidateStatus.NeedsReview;
                    audit.Add(enrollment.CampaignId, enrollment.Id, "LinkedInAttachmentRequiresReview");
                }
            }
            else
            {
                var result = await receive.HandleAsync(enrollment.Id, new ReceiveReplyRequest(item.ExternalMessageId, item.Content), ct, item.OccurredAt);
                if (!result.IsSuccess)
                {
                    unitOfWork.Clear();
                    await inbox.DeferAsync(id, "ReplyIngestion" + result.Status, ct);

                    return;
                }
            }

            item.ProcessedAt = DateTimeOffset.UtcNow;
            item.LeaseUntil = null;
            item.LastError = null;
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            unitOfWork.Clear();
            await inbox.DeferAsync(id, exception.GetType().Name, ct);
        }
    }
}