using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class RejectInterviewInvitationsHandler(
    ICampaignRepository campaigns,
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IValidator<RejectInterviewInvitationsRequest> validator)
{
    public async Task<Result<InterviewInvitationDecisionResponse>> HandleAsync(Guid id, RejectInterviewInvitationsRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<InterviewInvitationDecisionResponse>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<InterviewInvitationDecisionResponse>.NotFound("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Stopped)
            return Result<InterviewInvitationDecisionResponse>.Conflict("Campaign is stopped.");

        var selected = await enrollments.GetSelectedAsync(id, request.EnrollmentIds, ct);

        if (selected.Count != request.EnrollmentIds.Count)
            return Result<InterviewInvitationDecisionResponse>.NotFound("Some selected candidates do not belong to this campaign.");
        if (selected.Any(x => x.Status != CandidateStatus.Interested || !x.HasSentInitialMessage || x.HasSentInvitation ||
                              x.ApprovalStatus == OutreachApprovalStatus.Excluded))
            return Result<InterviewInvitationDecisionResponse>.Conflict("Select interested candidates whose interview invitation has not been sent. No decisions were saved.");

        foreach (var enrollment in selected)
        {
            var outgoing = await messages.FindOutgoingAsync(enrollment.Id, MessageKind.Invitation, ct);

            if (outgoing?.Delivery == DeliveryStatus.Sending)
                return Result<InterviewInvitationDecisionResponse>.Conflict("An invitation is already being sent. No decisions were saved.");
        }

        foreach (var enrollment in selected.Where(x => x.InvitationApprovalStatus != InvitationApprovalStatus.Rejected))
        {
            enrollment.InvitationApprovalStatus = InvitationApprovalStatus.Rejected;
            enrollment.InvitationApprovedAt = null;
            audit.Add(id, enrollment.Id, "InterviewInvitationRejected", "Manual decision");
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result<InterviewInvitationDecisionResponse>.Success(new InterviewInvitationDecisionResponse(request.EnrollmentIds, InvitationApprovalStatus.Rejected));
    }
}