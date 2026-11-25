using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileProfilesAdapter(
    IUnipileProfilesApi profiles,
    IOptions<LinkedInOptions> options,
    IValidator<CandidateProfile> validator) : ILinkedInProfiles
{
    public async Task<CandidateProfile> GetProfileAsync(string identifier, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(options.Value.AccountId, identifier, ct);
        var url = profile.PublicProfileUrl;
        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(profile.PublicIdentifier))
            url = "https://www.linkedin.com/in/" + Uri.EscapeDataString(profile.PublicIdentifier);
        var experience = string.Join(
            "\n",
            new[] { profile.Summary ?? "" }.Concat(
                (profile.WorkExperience ?? []).Select(x => $"{x.Position} — {x.Company} ({x.Start} – {x.End}): {x.Description}")));
        var result = new CandidateProfile(
            profile.ProviderId,
            url ?? "",
            $"{profile.FirstName} {profile.LastName}".Trim(),
            profile.Headline ?? "",
            profile.Location ?? "",
            experience);

        if (!(await validator.ValidateAsync(result, ct)).IsValid)
            throw new ApiException(502, "Unipile returned an incomplete or invalid LinkedIn profile.");

        return result;
    }
}