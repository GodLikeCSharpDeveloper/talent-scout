using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public interface IUnipileAdministrationApi
{
    Task<UnipilePage<UnipileAccount>> GetAccountsAsync(string? cursor, CancellationToken ct);
    Task<UnipilePage<UnipileRegisteredWebhook>> GetWebhooksAsync(string? cursor, CancellationToken ct);
    Task<UnipileWebhookResponse> CreateWebhookAsync(UnipileWebhookCreateRequest request, CancellationToken ct);
}