using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Conversations.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class ReceiveReplyHandler(IEnrollmentRepository enrollments, IMessageRepository messages, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<ReceiveReplyRequest> validator)
{
    public async Task<Result<Message>> HandleAsync(Guid id, ReceiveReplyRequest request, CancellationToken ct, DateTimeOffset? receivedAt = null)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Message>.Invalid(validation.AsErrors());

        var externalId = request.ExternalMessageId.Trim();
        var content = request.Content.Trim();
        var enrollment = await enrollments.FindAsync(id, ct);

        if (enrollment is null)
            return Result<Message>.NotFound("Enrollment was not found.");

        var duplicate = await messages.FindByExternalIdAsync(externalId, ct);
        if (duplicate is not null)
        {
            if (duplicate.EnrollmentId != id || duplicate.Kind != MessageKind.Reply || duplicate.Content != content)
                return Result<Message>.Conflict("External message ID already belongs to another payload.");

            return Result<Message>.Success(duplicate);
        }

        if (!enrollment.HasSentInitialMessage)
            return Result<Message>.Conflict("The initial message must be confirmed as sent before ingesting replies.");

        var message = new Message { EnrollmentId = id, ExternalId = externalId, Content = content, Kind = MessageKind.Reply, Delivery = DeliveryStatus.Received, CreatedAt = receivedAt ?? DateTimeOffset.UtcNow };
        // Touch the concurrency token so an in-flight processor cannot overwrite a newer reply.
        enrollment.Version = Guid.NewGuid();
        enrollment.NextContactAt = null;
        messages.Add(message);
        audit.Add(enrollment.CampaignId, id, "ReplyReceived");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Message>.Success(message);
    }
}