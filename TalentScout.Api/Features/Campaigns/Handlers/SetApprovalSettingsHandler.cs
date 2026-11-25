using Ardalis.Result.FluentValidation;
using FluentValidation;
using Ardalis.Result;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class SetApprovalSettingsHandler(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<SetApprovalSettingsRequest> validator)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, SetApprovalSettingsRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<Campaign>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<Campaign>.NotFound("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Stopped)
            return Result<Campaign>.Conflict("Campaign is stopped.");
        var autoCandidates = request.AutoApproveCandidates ?? campaign.AutoApproveCandidates;
        var autoInvitations = request.AutoApproveInterviewInvitations ?? campaign.AutoApproveInterviewInvitations;
        if (campaign.AutoApproveCandidates == autoCandidates && campaign.AutoApproveInterviewInvitations == autoInvitations)
            return Result<Campaign>.Success(campaign);

        campaign.AutoApproveCandidates = autoCandidates;
        campaign.AutoApproveInterviewInvitations = autoInvitations;
        audit.Add(id, null, "ApprovalSettingsChanged", $"Candidates={autoCandidates}; InterviewInvitations={autoInvitations}");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(campaign);
    }
}