using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Interviews.Handlers;
using TalentScout.Api.Features.Interviews.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Interviews;

[ApiController]
[TranslateResultToActionResult]
[Route("api/interviews")]
public sealed class InterviewsController(
    RecordBookingHandler recordBookingHandler,
    GetInterviewsHandler getInterviewsHandler) : ControllerBase
{
    [HttpPost("bookings")]
    public async Task<Result<Interview>> RecordBooking(RecordBookingRequest request, CancellationToken ct)
    {
        return await recordBookingHandler.HandleAsync(request, ct);
    }

    [HttpGet]
    public async Task<Result<List<Interview>>> GetAll([FromQuery] GetInterviewsRequest request, CancellationToken ct)
    {
        return await getInterviewsHandler.HandleAsync(request, ct);
    }
}