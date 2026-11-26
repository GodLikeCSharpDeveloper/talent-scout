using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;
using IResult = Ardalis.Result.IResult;

namespace TalentScout.Api.Features.Pipeline;

public sealed class SendPipelineMessagesHandler(
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    StartFirstContact firstContact,
    SendInterviewInvitationHandler invite)
{
    public async Task<IResult?> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null || enrollment.Campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped ||
            enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return null;

        // An incoming reply takes priority over any new outgoing message.
        if (await messages.GetNextUnanalyzedReplyAsync(id, ct) is not null)
            return null;

        if (enrollment.Status == CandidateStatus.Screened &&
            (enrollment.Campaign.Status == CampaignStatus.Active || enrollment.Campaign.AutoApproveCandidates))
        {
            var approved = enrollment.Campaign.AutoApproveCandidates ||
                           (enrollment.ApprovalStatus == OutreachApprovalStatus.Approved && enrollment.OutreachStartedAt.HasValue);

            if (approved)
                return await firstContact.HandleAsync(enrollment, ct);
        }

        if (enrollment.Status == CandidateStatus.Interested && enrollment.Campaign.Status == CampaignStatus.Active &&
            enrollment.InvitationApprovalStatus != InvitationApprovalStatus.Rejected &&
            (enrollment.Campaign.AutoApproveInterviewInvitations || enrollment.InvitationApprovalStatus == InvitationApprovalStatus.Approved))
            return await invite.HandleAsync(id, ct);

        return null;
    }
}