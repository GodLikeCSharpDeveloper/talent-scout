using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Interviews.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Interviews.Handlers;

public sealed class GetInterviewsHandler(IInterviewRepository interviews, IValidator<GetInterviewsRequest> validator)
{
    public async Task<Result<List<Interview>>> HandleAsync(GetInterviewsRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<Interview>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        return Result<List<Interview>>.Success(await interviews.GetPageAsync(request.CampaignId, skip, take, ct));
    }
}