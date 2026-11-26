using Ardalis.Result;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed record RetryLinkedInOutreachRequest(bool ConfirmNotSent = false);

public sealed record RetryLinkedInOutreachResponse(string Outcome);

public sealed class RetryLinkedInOutreachHandler(
    SynchronizeLinkedInHandler synchronize,
    IEnrollmentRepository enrollments,
    ILinkedInConversationRepository conversations,
    IMessageRepository messages,
    IUnitOfWork unitOfWork,
    IAuditRepository audit,
    SendPipelineMessagesHandler send,
    SendConnectionRequestHandler connect)
{
    public async Task<Result<RetryLinkedInOutreachResponse>> HandleAsync(Guid id, RetryLinkedInOutreachRequest request, CancellationToken ct)
    {
        var synchronized = await synchronize.HandleAsync(id, ct);

        if (!synchronized.IsSuccess)
            return Result<RetryLinkedInOutreachResponse>.Conflict("Synchronization must succeed before retrying.");

        var enrollment = (await enrollments.GetDetailsAsync(id, ct))!;

        if (enrollment.Campaign.Status != CampaignStatus.Active || enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return Result<RetryLinkedInOutreachResponse>.Conflict("Activate this campaign and approve the candidate before retrying.");
        if (!enrollment.Campaign.AutoApproveCandidates && enrollment.ApprovalStatus != OutreachApprovalStatus.Approved)
            return Result<RetryLinkedInOutreachResponse>.Conflict("Approve this candidate before retrying.");

        var conversation = await conversations.FindForEnrollmentAsync(id, ct);

        if (conversation?.ConnectionState == "Declined")
            return Result<RetryLinkedInOutreachResponse>.Conflict("Candidate declined the connection request. Resending is blocked.");
        if (conversation?.ConnectionState == "Inactive")
            return Result<RetryLinkedInOutreachResponse>.Conflict("The request is inactive, but the reason is unknown. Resending is blocked.");

        var withdrawn = conversation?.ConnectionState == "Withdrawn";

        if (conversation?.ConnectionState == "Pending")
            return Result<RetryLinkedInOutreachResponse>.Success(new RetryLinkedInOutreachResponse("AlreadySent"));

        var outgoing = await messages.GetForSynchronizationAsync(id, ct);
        var unresolved = outgoing.Where(x => x.Kind is MessageKind.Initial or MessageKind.Invitation &&
                                             x.Delivery is DeliveryStatus.Unknown or DeliveryStatus.Sending).ToArray();
        var uncertainInvitation = conversation?.ConnectionState is not ("Connected" or "Withdrawn") && conversation?.ConnectionRequestDelivery is DeliveryStatus.Unknown or DeliveryStatus.Sending;
        if (unresolved.Length > 0 || uncertainInvitation)
        {
            var recent = unresolved.Any(x => x.AttemptedAt > DateTimeOffset.UtcNow.AddMinutes(-5)) ||
                         (uncertainInvitation && conversation?.ConnectionRequestedAt > DateTimeOffset.UtcNow.AddMinutes(-5));

            if (recent)
                return Result<RetryLinkedInOutreachResponse>.Conflict("Wait five minutes after an unresolved attempt before retrying.");
            if (!request.ConfirmNotSent)
                return Result<RetryLinkedInOutreachResponse>.Conflict("Delivery is still uncertain. Verify in LinkedIn and confirm that the previous attempt was not sent.");

            foreach (var message in unresolved)
                await messages.ResetForRetryAsync(message.Id, ct);
        }

        if (conversation is not null && (withdrawn || uncertainInvitation) &&
            !await conversations.ResetInvitationForRetryAsync(conversation.Id, ct))
            return Result<RetryLinkedInOutreachResponse>.Conflict("Connection request changed. Synchronize before retrying.");

        enrollment.NextContactAt = null;
        audit.Add(enrollment.CampaignId, id, "LinkedInRetryRequested", request.ConfirmNotSent ? "Explicitly confirmed not sent" : "Synchronized before retry");
        await unitOfWork.SaveChangesAsync(ct);
        var result = withdrawn
            ? await connect.HandleAsync(id, new SendConnectionRequest(GenerateNote: true), ct)
            : await send.HandleAsync(id, ct);

        if (result is not null && result.Status is not (ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent))
            return Result<RetryLinkedInOutreachResponse>.Conflict("Retry is not available at the candidate's current stage.");

        return Result<RetryLinkedInOutreachResponse>.Success(new RetryLinkedInOutreachResponse("Processed"));
    }
}