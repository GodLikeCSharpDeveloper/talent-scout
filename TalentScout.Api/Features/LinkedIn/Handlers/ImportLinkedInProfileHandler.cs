using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Candidates.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class ImportLinkedInProfileHandler(ILinkedInProfiles profiles, CandidateImporter importer, IValidator<ImportLinkedInProfileRequest> validator)
{
    public async Task<Result<Candidate>> HandleAsync(ImportLinkedInProfileRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Candidate>.Invalid(validation.AsErrors());

        return Result<Candidate>.Success(await importer.ImportAsync(await profiles.GetProfileAsync(request.Identifier.Trim(), ct), ct));
    }
}