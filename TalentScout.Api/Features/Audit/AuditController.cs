using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Audit.Handlers;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Audit;

[ApiController]
[TranslateResultToActionResult]
[Route("api/campaigns/{campaignId:guid}/events")]
public sealed class AuditController(GetAuditEventsHandler getAuditEventsHandler) : ControllerBase
{
    [HttpGet]
    public async Task<Result<List<AuditEvent>>> GetAll(Guid campaignId, [FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return await getAuditEventsHandler.HandleAsync(campaignId, request, ct);
    }
}