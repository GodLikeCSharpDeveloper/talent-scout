using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class CandidateImporter(ICandidateRepository candidates, IUnitOfWork unitOfWork, IValidator<CandidateProfile> validator)
{
    public async Task<Candidate> ImportAsync(CandidateProfile profile, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(profile, ct);
        var externalId = profile.ExternalId.Trim();
        var url = new Uri(profile.ProfileUrl.Trim());

        var normalized = $"https://www.linkedin.com{url.AbsolutePath.TrimEnd('/').ToLowerInvariant()}";
        var candidate = await candidates.FindByIdentityAsync(externalId, normalized, ct);
        if (candidate is not null)
        {
            if (candidate.ExternalId != externalId)
                throw ApiException.Conflict("This profile is already mapped to another external identifier.");

            return candidate;
        }

        candidate = new Candidate
        {
            ExternalId = externalId,
            ProfileUrl = normalized,
            Name = profile.Name.Trim(),
            Headline = profile.Headline.Trim(),
            Location = profile.Location.Trim(),
            Experience = profile.Experience.Trim()
        };
        candidates.Add(candidate);
        await unitOfWork.SaveChangesAsync(ct);

        return candidate;
    }
}