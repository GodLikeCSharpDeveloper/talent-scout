using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class SetCampaignAiSettingsHandler(
    ICampaignRepository campaigns,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IValidator<SetCampaignAiSettingsRequest> validator)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, SetCampaignAiSettingsRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Campaign>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<Campaign>.NotFound("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Stopped)
            return Result<Campaign>.Conflict("Campaign is stopped.");

        var prompt = request.SystemPrompt.Trim();

        if (campaign.SystemPrompt == prompt)
            return Result<Campaign>.Success(campaign);

        campaign.SystemPrompt = prompt;
        audit.Add(id, null, "CampaignAiSettingsChanged");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(campaign);
    }
}