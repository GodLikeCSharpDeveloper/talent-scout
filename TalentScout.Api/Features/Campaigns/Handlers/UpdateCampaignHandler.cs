using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class UpdateCampaignHandler(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<CreateCampaignRequest> validator)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, CreateCampaignRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Campaign>.Invalid(validation.AsErrors());

        var c = await campaigns.FindAsync(id, ct);

        if (c is null)
            return Result<Campaign>.NotFound("Campaign was not found.");
        if (c.Status is not (CampaignStatus.Draft or CampaignStatus.Paused))
            return Result<Campaign>.Conflict("Pause the campaign before editing. Stopped campaigns cannot be edited.");

        c.Name = request.Name.Trim();
        c.JobTitle = request.JobTitle.Trim();
        c.Description = request.Description.Trim();
        c.Requirements = request.Requirements.Trim();
        c.SearchQuery = request.SearchQuery.Trim();
        c.Location = request.Location.Trim();
        c.LocationId = string.IsNullOrWhiteSpace(request.LocationId) ? null : request.LocationId.Trim();
        c.Recruiter = request.Recruiter.Trim();
        c.InterviewUrl = string.IsNullOrWhiteSpace(request.InterviewUrl) ? "" : new Uri(request.InterviewUrl.Trim()).AbsoluteUri;
        c.MinimumFitScore = request.MinimumFitScore;
        c.SystemPrompt = request.SystemPrompt.Trim();
        c.AutoApproveCandidates = request.AutoApproveCandidates;
        c.AutoApproveInterviewInvitations = request.AutoApproveInterviewInvitations;
        audit.Add(id, null, "CampaignUpdated", "Existing assessments and messages were retained; changes apply to subsequent operations.");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(c);
    }
}