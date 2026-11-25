using Ardalis.Result;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class GetCampaignHandler(ICampaignRepository campaigns)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, CancellationToken ct)
    {
        var campaign = await campaigns.FindAsync(id, ct);

        return campaign is null
            ? Result<Campaign>.NotFound("Campaign was not found.")
            : Result<Campaign>.Success(campaign);
    }
}