using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Candidates.Requests;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class ImportCandidateHandler(CandidateImporter importer, IValidator<ImportCandidateRequest> validator)
{
    public async Task<Result<Candidate>> HandleAsync(ImportCandidateRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Candidate>.Invalid(validation.AsErrors());

        return Result<Candidate>.Success(await importer.ImportAsync(new CandidateProfile(request.ExternalId, request.ProfileUrl, request.Name, request.Headline, request.Location, request.Experience), ct));
    }
}