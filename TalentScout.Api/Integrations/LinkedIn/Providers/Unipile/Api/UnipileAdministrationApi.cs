using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public sealed class UnipileAdministrationApi(IUnipileHttpClient http) : IUnipileAdministrationApi
{
    public Task<UnipilePage<UnipileAccount>> GetAccountsAsync(string? cursor, CancellationToken ct)
    {
        return http.GetAsync<UnipilePage<UnipileAccount>>(PagePath("accounts", cursor), ct);
    }

    public Task<UnipilePage<UnipileRegisteredWebhook>> GetWebhooksAsync(string? cursor, CancellationToken ct)
    {
        return http.GetAsync<UnipilePage<UnipileRegisteredWebhook>>(PagePath("webhooks", cursor), ct);
    }

    public Task<UnipileWebhookResponse> CreateWebhookAsync(UnipileWebhookCreateRequest request, CancellationToken ct)
    {
        return http.PostAsync<UnipileWebhookResponse>("webhooks", request, ct);
    }

    private static string PagePath(string resource, string? cursor)
    {
        return resource + "?limit=100" + (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
    }
}