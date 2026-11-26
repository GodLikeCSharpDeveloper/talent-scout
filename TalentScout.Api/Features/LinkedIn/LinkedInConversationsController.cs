using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Features.LinkedIn;

[ApiController]
[TranslateResultToActionResult]
[Route("api/enrollments/{id:guid}/linkedin")]
public sealed class LinkedInConversationsController(
    GetLinkedInConversationHandler getConversation,
    BindLinkedInConversationHandler bindConversation,
    SendConnectionRequestHandler sendConnection,
    SynchronizeLinkedInHandler synchronize,
    RetryLinkedInOutreachHandler retry,
    GetLinkedInContactStatusHandler getContactStatus) : ControllerBase
{
    [HttpPost("sync")]
    public Task<Result<LinkedInConversationResponse>> Synchronize(Guid id, CancellationToken ct)
    {
        return synchronize.HandleAsync(id, ct);
    }

    [HttpPost("retry")]
    public Task<Result<RetryLinkedInOutreachResponse>> Retry(Guid id, RetryLinkedInOutreachRequest request, CancellationToken ct)
    {
        return retry.HandleAsync(id, request, ct);
    }

    [HttpGet("conversation")]
    public Task<Result<LinkedInConversationResponse>> GetConversation(Guid id, CancellationToken ct)
    {
        return getConversation.HandleAsync(id, ct);
    }

    [HttpPut("conversation")]
    public Task<Result<LinkedInConversationResponse>> BindConversation(Guid id, BindLinkedInConversationRequest request, CancellationToken ct)
    {
        return bindConversation.HandleAsync(id, request, ct);
    }

    [HttpGet("contact-status")]
    public Task<Result<LinkedInContactStatus>> GetContactStatus(Guid id, CancellationToken ct)
    {
        return getContactStatus.HandleAsync(id, ct);
    }

    [HttpPost("connection-request")]
    public Task<Result<LinkedInConversationResponse>> SendConnection(Guid id, SendConnectionRequest request, CancellationToken ct)
    {
        return sendConnection.HandleAsync(id, request, ct);
    }
}