using FluentValidation;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class MessageDispatcher(IEnrollmentRepository enrollments, IMessageRepository messages, IAuditRepository audit, IUnitOfWork unitOfWork, ILinkedInGateway linkedIn, IOptions<PipelineOptions> options, ILogger<MessageDispatcher> logger, IValidator<SentMessage> validator, ILinkedInConversationRepository conversations, ILinkedInContacts contacts, IOptions<LinkedInOptions> linkedInOptions)
{
    public async Task DispatchAsync(Enrollment enrollment, Message message, CancellationToken ct)
    {
        // Configuration errors have no side effect and must not claim a delivery attempt.
        if (!linkedInOptions.Value.IsConfigured)
            throw new ApiException(503, "LinkedIn provider is not configured.");

        if (message.Delivery != DeliveryStatus.Pending)
            return;

        LinkedInConversation? conversation = null;
        if (!string.IsNullOrWhiteSpace(linkedInOptions.Value.AccountId))
        {
            var accountId = linkedInOptions.Value.AccountId;
            conversation = await conversations.ReserveAsync(enrollment.Id, accountId, enrollment.Candidate.ExternalId, ct);

            if (conversation.EnrollmentId != enrollment.Id || conversation.AccountId != accountId)
                throw new ApiException(409, "This LinkedIn conversation is already assigned to another campaign.");

            if (conversation.ChatId is null && conversation.ConnectionConfirmedAt is null)
            {
                var contact = await contacts.GetStatusAsync(enrollment.Candidate.ExternalId, ct);
                if (!contact.CanMessage)
                {
                    await enrollments.ScheduleNextAttemptAsync(enrollment.Id, DateTimeOffset.UtcNow.AddHours(6), ct);

                    throw new ApiException(409, "Candidate is not a LinkedIn connection. Send a connection request and wait for acceptance, or enable InMail.");
                }
            }
        }

        var now = DateTimeOffset.UtcNow;

        if (!await messages.TryClaimDeliveryAsync(
                enrollment,
                message,
                now,
                options.Value.MinimumSendIntervalSeconds,
                options.Value.MaximumDailyMessagesPerCampaign,
                ct))
            return;

        await messages.ReloadAsync(message, ct);
        try
        {
            var sent = await linkedIn.SendAsync(new OutgoingMessage(enrollment.Candidate.ExternalId, message.Content, message.Id.ToString(), conversation?.ChatId), ct);

            if (sent is null || !(await validator.ValidateAsync(sent, ct)).IsValid)
                throw new ApiException(502, "LinkedIn returned an invalid message identifier.");

            if (conversation is not null)
            {
                conversation.ChatId = sent.ChatId ?? conversation.ChatId;
                conversation.ProviderChatId = sent.ProviderChatId ?? conversation.ProviderChatId;
            }

            message.ExternalId = sent.ExternalId.Trim();
            message.Delivery = DeliveryStatus.Sent;
            message.SentAt = DateTimeOffset.UtcNow;
            // Reload to preserve any reply or booking that arrived while the request was in flight.
            await enrollments.ReloadAsync(enrollment, ct);
            if (message.Kind == MessageKind.Initial)
            {
                enrollment.HasSentInitialMessage = true;
                if (enrollment.Status == CandidateStatus.Screened)
                    enrollment.Status = CandidateStatus.AwaitingReply;
            }
            else
            {
                enrollment.HasSentInvitation = true;
                if (enrollment.Status == CandidateStatus.Interested)
                    enrollment.Status = CandidateStatus.InterviewInvitationSent;
            }

            audit.Add(enrollment.CampaignId, enrollment.Id, "MessageSent", message.Kind.ToString());
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Delivery outcome is unknown for message {MessageId}; exception type {ExceptionType}. No automatic retry.", message.Id, exception.GetType().Name);
            // Sending also remains non-retryable if this recovery write fails or the process dies.
            unitOfWork.Clear();
            await messages.MarkDeliveryUnknownAsync(message.Id, CancellationToken.None);
            audit.Add(enrollment.CampaignId, enrollment.Id, "DeliveryOutcomeUnknown", message.Id.ToString());
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            throw new ApiException(503, "Delivery outcome is unknown. Reconcile with the provider before taking further action.");
        }
    }
}