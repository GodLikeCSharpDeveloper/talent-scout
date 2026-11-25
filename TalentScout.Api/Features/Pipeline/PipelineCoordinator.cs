using TalentScout.Api.Features.Candidates.Handlers;
using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;
using IResult = Ardalis.Result.IResult;

namespace TalentScout.Api.Features.Pipeline;

public sealed class PipelineCoordinator(
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    ScreenProfileHandler screen,
    SendIntroductionHandler introduce,
    AnalyzeReplyHandler analyze,
    SendInterviewInvitationHandler invite)
{
    public async Task<IResult?> AdvanceAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null || enrollment.Campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped)
            return null;
        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return null;
        if (enrollment.Status == CandidateStatus.Found)
            return await screen.HandleAsync(id, ct);
        if (enrollment.Campaign.Status == CampaignStatus.Active && await messages.GetNextUnanalyzedReplyAsync(id, ct) is not null)
            return await analyze.HandleAsync(id, ct);
        if (enrollment.Status == CandidateStatus.Screened &&
            (enrollment.Campaign.Status == CampaignStatus.Active || enrollment.Campaign.AutoApproveCandidates) &&
            (enrollment.Campaign.AutoApproveCandidates ||
             (enrollment.ApprovalStatus == OutreachApprovalStatus.Approved && enrollment.OutreachStartedAt.HasValue)))
            return await introduce.HandleAsync(id, ct);
        if (enrollment.Campaign.Status == CampaignStatus.Active && enrollment.Status == CandidateStatus.Interested &&
            enrollment.InvitationApprovalStatus != InvitationApprovalStatus.Rejected &&
            (enrollment.Campaign.AutoApproveInterviewInvitations || enrollment.InvitationApprovalStatus == InvitationApprovalStatus.Approved))
            return await invite.HandleAsync(id, ct);

        return null;
    }
}