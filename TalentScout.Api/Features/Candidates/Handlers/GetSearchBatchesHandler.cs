using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Candidates.Responses;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class GetSearchBatchesHandler(ICampaignRepository campaigns, ISearchBatchRepository batches, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<SearchBatchResponse>>> HandleAsync(Guid id, PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<SearchBatchResponse>>.Invalid(validation.AsErrors());
        if (!await campaigns.ExistsAsync(id, ct))
            return Result<List<SearchBatchResponse>>.NotFound("Campaign was not found.");

        var records = await batches.GetPageAsync(id, (request.Page - 1) * request.PageSize, request.PageSize, ct);

        return Result<List<SearchBatchResponse>>.Success(
            records.Select(x => new SearchBatchResponse(
                x.Id,
                x.RequestedCount,
                x.AddedCount,
                x.SkippedCount,
                x.ScannedCount,
                x.PagesScanned,
                x.Status,
                x.StopReason,
                x.NextCursor,
                x.CreatedAt,
                x.CompletedAt)).ToList());
    }
}