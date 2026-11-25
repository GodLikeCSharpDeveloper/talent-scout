using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public sealed class UnipileSearchApi(IUnipileHttpClient http) : IUnipileSearchApi
{
    public Task<UnipilePage<UnipileSearchItem>> SearchAsync(UnipilePeopleSearchRequest request, CancellationToken ct)
    {
        var path = $"linkedin/search?account_id={Uri.EscapeDataString(request.AccountId)}&limit={Math.Min(request.Limit, 10)}";
        if (!string.IsNullOrEmpty(request.Cursor))
            path += "&cursor=" + Uri.EscapeDataString(request.Cursor);

        return http.PostAsync<UnipilePage<UnipileSearchItem>>(
            path,
            new
            {
                api = "classic", category = "people", keywords = request.Keywords,
                location = request.LocationIds.Count == 0 ? null : request.LocationIds
            },
            ct);
    }

    public Task<UnipilePage<UnipileSearchParameter>> FindLocationsAsync(string accountId, string query, CancellationToken ct)
    {
        return http.GetAsync<UnipilePage<UnipileSearchParameter>>(
            $"linkedin/search/parameters?account_id={Uri.EscapeDataString(accountId)}&type=LOCATION&keywords={Uri.EscapeDataString(query)}&limit=10",
            ct);
    }
}