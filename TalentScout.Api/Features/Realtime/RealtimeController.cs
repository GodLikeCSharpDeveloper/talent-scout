using Microsoft.AspNetCore.Mvc;

namespace TalentScout.Api.Features.Realtime;

[ApiController]
[Route("api/realtime")]
public sealed class RealtimeController(RealtimeTickets tickets) : ControllerBase
{
    [HttpPost("ticket")]
    public object IssueTicket()
    {
        Response.Headers.CacheControl = "no-store";

        return new { ticket = tickets.Issue(), expiresInSeconds = 60 };
    }
}