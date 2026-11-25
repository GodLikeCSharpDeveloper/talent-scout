using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Candidates.Handlers;
using TalentScout.Api.Features.Candidates.Requests;
using TalentScout.Api.Features.Candidates.Responses;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Candidates;

[ApiController]
[TranslateResultToActionResult]
[Route("api/candidates")]
public sealed class CandidatesController(
    ImportCandidateHandler importCandidateHandler,
    GetCandidatesHandler getCandidatesHandler,
    GetCandidateHandler getCandidateHandler,
    SearchCandidatesHandler searchCandidatesHandler,
    ScreenProfileHandler screenProfileHandler,
    GetSearchBatchesHandler getSearchBatchesHandler) : ControllerBase
{
    [HttpPost]
    public async Task<Result<Candidate>> Import(ImportCandidateRequest request, CancellationToken ct)
    {
        return await importCandidateHandler.HandleAsync(request, ct);
    }

    [HttpGet]
    public async Task<Result<List<Candidate>>> GetAll([FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return await getCandidatesHandler.HandleAsync(request, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<Result<Candidate>> Get(Guid id, CancellationToken ct)
    {
        return await getCandidateHandler.HandleAsync(id, ct);
    }

    [HttpPost("/api/campaigns/{id:guid}/search")]
    public async Task<Result<SearchCandidatesResponse>> Search(Guid id, SearchCandidatesRequest request, CancellationToken ct)
    {
        return await searchCandidatesHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/enrollments/{id:guid}/screen-profile")]
    public async Task<Result<ScreenProfileResponse>> ScreenProfile(Guid id, CancellationToken ct)
    {
        return await screenProfileHandler.HandleAsync(id, ct);
    }

    [HttpGet("/api/campaigns/{id:guid}/search-batches")]
    public async Task<Result<List<SearchBatchResponse>>> GetSearchBatches(Guid id, [FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return await getSearchBatchesHandler.HandleAsync(id, request, ct);
    }
}