using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileSearchParametersAdapter(IUnipileSearchApi search, IOptions<LinkedInOptions> options) : ILinkedInSearchParameters
{
    public async Task<IReadOnlyList<LinkedInSearchLocation>> FindLocationsAsync(string query, CancellationToken ct)
    {
        var page = await search.FindLocationsAsync(options.Value.AccountId, query, ct);

        return (page.Items ?? []).Select(x => new LinkedInSearchLocation(x.Id, x.Title)).ToList();
    }
}