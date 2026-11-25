using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class ChangeCampaignStatusHandler(ICampaignRepository campaigns, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<ChangeStatusRequest> validator)
{
    public async Task<Result<Campaign>> HandleAsync(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Campaign>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<Campaign>.NotFound("Campaign was not found.");

        if (campaign.Status == request.Status)
            return Result<Campaign>.Success(campaign);

        var allowed = (campaign.Status, request.Status) switch
        {
            (CampaignStatus.Draft, CampaignStatus.Active or CampaignStatus.Stopped) => true,
            (CampaignStatus.Active, CampaignStatus.Paused or CampaignStatus.Stopped) => true,
            (CampaignStatus.Paused, CampaignStatus.Active or CampaignStatus.Stopped) => true,
            _ => false
        };

        if (!allowed)
            return Result<Campaign>.Conflict("Invalid campaign status transition. Stopped campaigns cannot be restarted.");

        campaign.Status = request.Status;
        audit.Add(id, null, "CampaignStatusChanged", request.Status.ToString());
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Campaign>.Success(campaign);
    }
}