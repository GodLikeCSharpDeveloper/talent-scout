using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.LinkedIn;

[ApiController]
[TranslateResultToActionResult]
[Route("api/integrations/linkedin")]
public sealed class LinkedInIntegrationController(
    GetLinkedInIntegrationStatusHandler getStatus,
    GetLinkedInAccountsHandler getAccounts,
    ImportLinkedInProfileHandler importProfile,
    GetUnipileWebhookEventsHandler getEvents) : ControllerBase
{
    [HttpGet]
    public Result<LinkedInIntegrationStatusResponse> GetStatus()
    {
        return getStatus.Handle();
    }

    [HttpGet("accounts")]
    public Task<Result<IReadOnlyList<LinkedInAccount>>> GetAccounts(CancellationToken ct)
    {
        return getAccounts.HandleAsync(ct);
    }

    [HttpPost("profiles/import")]
    public Task<Result<Candidate>> ImportProfile(ImportLinkedInProfileRequest request, CancellationToken ct)
    {
        return importProfile.HandleAsync(request, ct);
    }

    [HttpGet("webhook-events")]
    public Task<Result<List<LinkedInWebhookEvent>>> GetWebhookEvents([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return getEvents.HandleAsync(request, ct);
    }
}