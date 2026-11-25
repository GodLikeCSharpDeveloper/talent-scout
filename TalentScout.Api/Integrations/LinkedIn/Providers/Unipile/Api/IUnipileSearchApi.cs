using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public interface IUnipileSearchApi
{
    Task<UnipilePage<UnipileSearchItem>> SearchAsync(UnipilePeopleSearchRequest request, CancellationToken ct);
    Task<UnipilePage<UnipileSearchParameter>> FindLocationsAsync(string accountId, string query, CancellationToken ct);
}