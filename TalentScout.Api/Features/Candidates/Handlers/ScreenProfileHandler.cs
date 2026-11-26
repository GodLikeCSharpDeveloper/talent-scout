using Ardalis.Result;
using TalentScout.Api.Features.Candidates.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class ScreenProfileHandler(IEnrollmentRepository enrollments, ProfileScreener screener)
{
    public async Task<Result<ScreenProfileResponse>> HandleAsync(Guid id, CancellationToken ct, bool reassess = false)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<ScreenProfileResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status == CampaignStatus.Stopped || (!reassess && enrollment.Campaign.Status == CampaignStatus.Paused))
            return Result<ScreenProfileResponse>.Conflict("A paused or stopped campaign cannot screen profiles.");
        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return Result<ScreenProfileResponse>.Conflict("Candidate was excluded from outreach.");

        if (reassess && (enrollment.Status is not (CandidateStatus.Found or CandidateStatus.Screened or CandidateStatus.Rejected) ||
                         enrollment.HasSentInitialMessage || enrollment.HasSentInvitation || enrollment.OutreachStartedAt is not null))
            return Result<ScreenProfileResponse>.Conflict("Only profiles before outreach has started can be reassessed. Conversations and interview stages are preserved.");

        if (enrollment.Status == CandidateStatus.Found || reassess)
            await screener.ScreenAsync(enrollment, ct);
        else if (!enrollment.FitScore.HasValue)
            return Result<ScreenProfileResponse>.Conflict("Only a newly found profile can be screened.");

        return Result<ScreenProfileResponse>.Success(new ScreenProfileResponse(enrollment.Id, enrollment.Status, enrollment.FitScore, enrollment.Assessment, enrollment.ApprovalStatus, enrollment.ApprovedAt, enrollment.OutreachStartedAt));
    }
}