using Ardalis.Result;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class GetCandidateHandler(ICandidateRepository candidates)
{
    public async Task<Result<Candidate>> HandleAsync(Guid id, CancellationToken ct)
    {
        var candidate = await candidates.FindAsync(id, ct);

        return candidate is null
            ? Result<Candidate>.NotFound("Candidate was not found.")
            : Result<Candidate>.Success(candidate);
    }
}