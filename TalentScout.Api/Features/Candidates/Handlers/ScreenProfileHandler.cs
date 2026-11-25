using Ardalis.Result;
using TalentScout.Api.Features.Candidates.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class ScreenProfileHandler(IEnrollmentRepository enrollments, ProfileScreener screener)
{
    public async Task<Result<ScreenProfileResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<ScreenProfileResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped)
            return Result<ScreenProfileResponse>.Conflict("A paused or stopped campaign cannot screen profiles.");
        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return Result<ScreenProfileResponse>.Conflict("Candidate was excluded from outreach.");

        if (enrollment.Status == CandidateStatus.Found)
            await screener.ScreenAsync(enrollment, ct);
        else if (!enrollment.FitScore.HasValue)
            return Result<ScreenProfileResponse>.Conflict("Only a newly found profile can be screened.");

        return Result<ScreenProfileResponse>.Success(new ScreenProfileResponse(enrollment.Id, enrollment.Status, enrollment.FitScore, enrollment.Assessment, enrollment.ApprovalStatus, enrollment.ApprovedAt, enrollment.OutreachStartedAt));
    }
}