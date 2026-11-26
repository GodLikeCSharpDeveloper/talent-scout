using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn;

public sealed class LinkedInSynchronizationService(ILinkedInSynchronization linkedIn, IMessageRepository messages)
{
    public async Task SynchronizeAsync(Enrollment enrollment, LinkedInConversation conversation, CancellationToken ct)
    {
        var snapshot = await linkedIn.ReadAsync(conversation.RecipientId, conversation.ChatId, ct);
        var wasConnected = conversation.ConnectionState == "Connected";
        UpdateConnection(conversation, snapshot);
        await AddMissingMessagesAsync(enrollment, snapshot.Messages, ct);
        if (!wasConnected && snapshot.Contact.Connected)
            enrollment.NextContactAt = null;
    }

    private async Task AddMissingMessagesAsync(Enrollment enrollment, IReadOnlyList<LinkedInSyncMessage> remote, CancellationToken ct)
    {
        var existingIds = await messages.GetExistingExternalIdsAsync(remote.Select(x => x.ExternalId).ToArray(), ct);
        foreach (var message in remote)
        {
            if (!existingIds.Add(message.ExternalId))
                continue;

            var analyzeReply = !message.IsOutgoing && enrollment.HasSentInitialMessage &&
                               message.Timestamp >= (enrollment.OutreachStartedAt ?? enrollment.CreatedAt) &&
                               !message.IsDeleted && !message.HasAttachments && !string.IsNullOrWhiteSpace(message.Text);
            messages.Add(
                new Message
                {
                    EnrollmentId = enrollment.Id,
                    ExternalId = message.ExternalId,
                    Kind = message.IsOutgoing ? MessageKind.Manual : MessageKind.Reply,
                    Content = message.IsDeleted ? "" : message.Text,
                    CreatedAt = message.Timestamp,
                    SentAt = message.IsOutgoing ? message.Timestamp : null,
                    Delivery = message.IsOutgoing ? DeliveryStatus.Sent : DeliveryStatus.Received,
                    IsDeleted = message.IsDeleted,
                    IsEdited = message.IsEdited,
                    Analyzed = !analyzeReply
                });
            if (analyzeReply)
                enrollment.NextContactAt = null;
        }
    }

    private static void UpdateConnection(LinkedInConversation conversation, LinkedInSyncSnapshot snapshot)
    {
        conversation.ConnectionConfirmedAt = snapshot.Contact.Connected ? conversation.ConnectionConfirmedAt ?? DateTimeOffset.UtcNow : null;
        if (snapshot.Chat is not null)
        {
            conversation.ChatId = snapshot.Chat.ChatId;
            conversation.ProviderChatId = snapshot.Chat.ProviderChatId;
        }

        conversation.ConnectionState = snapshot.Contact switch
        {
            { Connected: true } => "Connected",
            { SentInvitationStatus: ConnectionInvitationStatus.Declined } => "Declined",
            { SentInvitationStatus: ConnectionInvitationStatus.Withdrawn } => "Withdrawn",
            { InvitationPending: true } => "Pending",
            _ when conversation.ConnectionState is "Declined" or "Withdrawn" => conversation.ConnectionState,
            _ when conversation.ConnectionRequestDelivery == DeliveryStatus.Sent => "Inactive",
            _ when conversation.ConnectionRequestDelivery is DeliveryStatus.Unknown or DeliveryStatus.Sending => "ActionRequired",
            _ => "NotConnected"
        };

        if (conversation.ConnectionState != "Pending")
            return;

        conversation.ConnectionRequestDelivery = DeliveryStatus.Sent;
        conversation.InvitationId = snapshot.PendingInvitationId ?? conversation.InvitationId;
        conversation.ConnectionNote = snapshot.InvitationNote ?? conversation.ConnectionNote;
    }
}