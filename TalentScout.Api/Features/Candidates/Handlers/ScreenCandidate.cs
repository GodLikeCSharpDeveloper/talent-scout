using FluentValidation;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class ProfileScreener(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork, IRecruitmentAi ai, IValidator<ProfileAssessment> validator)
{
    public async Task ScreenAsync(Enrollment enrollment, CancellationToken ct)
    {
        var campaign = enrollment.Campaign;
        var candidate = enrollment.Candidate;
        var result = await ai.AssessProfileAsync(
            new Vacancy(campaign.JobTitle, campaign.Description, campaign.Requirements),
            new CandidateProfile(candidate.ExternalId, candidate.ProfileUrl, candidate.Name, candidate.Headline, candidate.Location, candidate.Experience),
            ct);

        if (result is null || !(await validator.ValidateAsync(result, ct)).IsValid)
            throw new ApiException(502, "AI returned an invalid profile score.");

        await campaigns.ReloadAsync(campaign, ct);
        enrollment.FitScore = result.Score;
        enrollment.Assessment = result.Reason.Trim();
        enrollment.Status = result.MeetsRequiredCriteria && result.Score >= campaign.MinimumFitScore
            ? CandidateStatus.Screened
            : CandidateStatus.Rejected;
        audit.Add(campaign.Id, enrollment.Id, "ProfileAssessed", enrollment.Status.ToString());
        await unitOfWork.SaveChangesAsync(ct);
    }
}