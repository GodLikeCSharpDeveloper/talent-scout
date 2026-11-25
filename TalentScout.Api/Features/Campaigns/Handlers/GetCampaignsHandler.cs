using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Campaigns.Handlers;

public sealed class GetCampaignsHandler(ICampaignRepository campaigns, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<Campaign>>> HandleAsync(PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<Campaign>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        return Result<List<Campaign>>.Success(await campaigns.GetPageAsync(skip, take, ct));
    }
}