using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class GetCandidatesHandler(ICandidateRepository candidates, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<Candidate>>> HandleAsync(PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<Candidate>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        return Result<List<Candidate>>.Success(await candidates.GetPageAsync(skip, take, ct));
    }
}