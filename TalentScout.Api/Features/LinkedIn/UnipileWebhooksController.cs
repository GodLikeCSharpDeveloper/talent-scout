using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;

namespace TalentScout.Api.Features.LinkedIn;

[ApiController]
[TranslateResultToActionResult]
[Route("api/webhooks/unipile")]
public sealed class UnipileWebhooksController(ReceiveUnipileWebhookHandler receive) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(524288)]
    public Task<Result<UnipileWebhookReceipt>> Receive(UnipileWebhookRequest request, CancellationToken ct)
    {
        return receive.HandleAsync(request, ct);
    }
}