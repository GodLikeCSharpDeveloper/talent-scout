using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Campaigns.Handlers;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Campaigns;

[ApiController]
[TranslateResultToActionResult]
[Route("api/campaigns")]
public sealed class CampaignsController(
    CreateCampaignHandler createCampaignHandler,
    GetCampaignsHandler getCampaignsHandler,
    GetCampaignHandler getCampaignHandler,
    ChangeCampaignStatusHandler changeCampaignStatusHandler,
    SetApprovalSettingsHandler setApprovalSettingsHandler,
    SetCampaignAiSettingsHandler setCampaignAiSettingsHandler) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Campaign), StatusCodes.Status201Created)]
    public async Task<Result<Campaign>> Create(CreateCampaignRequest request, CancellationToken ct)
    {
        var result = await createCampaignHandler.HandleAsync(request, ct);

        return result.IsSuccess ? Result<Campaign>.Created(result.Value, $"/api/campaigns/{result.Value.Id}") : result;
    }

    [HttpGet]
    public async Task<Result<List<Campaign>>> GetAll([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return await getCampaignsHandler.HandleAsync(request, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<Result<Campaign>> Get(Guid id, CancellationToken ct)
    {
        return await getCampaignHandler.HandleAsync(id, ct);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<Result<Campaign>> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        return await changeCampaignStatusHandler.HandleAsync(id, request, ct);
    }

    [HttpPut("{id:guid}/ai-settings")]
    public async Task<Result<Campaign>> SetAiSettings(Guid id, SetCampaignAiSettingsRequest request, CancellationToken ct)
    {
        return await setCampaignAiSettingsHandler.HandleAsync(id, request, ct);
    }

    [HttpPut("{id:guid}/approval-settings")]
    public async Task<Result<Campaign>> SetApprovalSettings(Guid id, SetApprovalSettingsRequest request, CancellationToken ct)
    {
        return await setApprovalSettingsHandler.HandleAsync(id, request, ct);
    }
}