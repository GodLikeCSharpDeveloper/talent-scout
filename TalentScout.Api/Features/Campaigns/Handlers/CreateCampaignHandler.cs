using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class CreateCampaignHandler(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<CreateCampaignRequest> validator)
{
    public async Task<Result<Campaign>> HandleAsync(CreateCampaignRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Campaign>.Invalid(validation.AsErrors());

        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            JobTitle = request.JobTitle.Trim(),
            Description = request.Description.Trim(),
            Requirements = request.Requirements.Trim(),
            SearchQuery = request.SearchQuery.Trim(),
            Location = request.Location.Trim(),
            Recruiter = request.Recruiter.Trim(),
            InterviewUrl = string.IsNullOrWhiteSpace(request.InterviewUrl) ? "" : new Uri(request.InterviewUrl.Trim()).AbsoluteUri,
            MinimumFitScore = request.MinimumFitScore,
            AutoApproveCandidates = request.AutoApproveCandidates,
            AutoApproveInterviewInvitations = request.AutoApproveInterviewInvitations
        };
        campaigns.Add(campaign);
        audit.Add(campaign.Id, null, "CampaignCreated");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(campaign);
    }
}