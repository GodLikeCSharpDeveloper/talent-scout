using FluentValidation;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class MessageDispatcher(IEnrollmentRepository enrollments, IMessageRepository messages, IAuditRepository audit, IUnitOfWork unitOfWork, ILinkedInGateway linkedIn, IOptions<PipelineOptions> options, ILogger<MessageDispatcher> logger, IValidator<SentMessage> validator)
{
    public async Task DispatchAsync(Enrollment enrollment, Message message, CancellationToken ct)
    {
        // Configuration errors have no side effect and must not claim a delivery attempt.
        if (!linkedIn.IsConfigured)
            throw new ApiException(503, "LinkedIn provider is not configured.");

        if (message.Delivery != DeliveryStatus.Pending)
            return;

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
            var sent = await linkedIn.SendAsync(new OutgoingMessage(enrollment.Candidate.ExternalId, message.Content, message.Id.ToString()), ct);

            if (sent is null || !(await validator.ValidateAsync(sent, ct)).IsValid)
                throw new ApiException(502, "LinkedIn returned an invalid message identifier.");

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