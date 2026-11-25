using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Conversations.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class ReconcileDeliveryHandler(IEnrollmentRepository enrollments, IMessageRepository messages, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<ReconcileDeliveryRequest> validator)
{
    public async Task<Result<Message>> HandleAsync(Guid id, ReconcileDeliveryRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Message>.Invalid(validation.AsErrors());

        var reason = request.Reason.Trim();
        var message = await messages.FindAsync(id, ct);

        if (message is null)
            return Result<Message>.NotFound("Message was not found.");

        if (message.Delivery is not (DeliveryStatus.Unknown or DeliveryStatus.Sending))
            return Result<Message>.Conflict("Only unresolved delivery attempts can be reconciled.");
        if (message.AttemptedAt > DateTimeOffset.UtcNow.AddMinutes(-5))
            return Result<Message>.Conflict("Wait at least five minutes after the attempt and verify its outcome with the provider.");

        var enrollment = await enrollments.FindAsync(message.EnrollmentId, ct);

        if (enrollment is null)
            return Result<Message>.NotFound("Enrollment was not found.");

        if (request.WasSent)
        {
            message.ExternalId = request.ExternalMessageId!.Trim();
            message.Delivery = DeliveryStatus.Sent;
            message.SentAt = message.AttemptedAt;
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
        }
        else
            message.Delivery = DeliveryStatus.Pending;

        audit.Add(enrollment.CampaignId, enrollment.Id, "DeliveryReconciled", $"WasSent={request.WasSent}: {reason}");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Message>.Success(message);
    }
}