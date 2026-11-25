using Ardalis.Result;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class SetApprovalSettingsHandler(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, SetApprovalSettingsRequest request, CancellationToken ct)
    {
        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<Campaign>.NotFound("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Stopped)
            return Result<Campaign>.Conflict("Campaign is stopped.");
        if (campaign.AutoApproveCandidates == request.AutoApproveCandidates)
            return Result<Campaign>.Success(campaign);

        campaign.AutoApproveCandidates = request.AutoApproveCandidates;
        audit.Add(id, null, "ApprovalSettingsChanged", $"AutoApproveCandidates={request.AutoApproveCandidates}");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(campaign);
    }
}