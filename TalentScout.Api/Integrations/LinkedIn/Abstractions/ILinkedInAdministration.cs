using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInAdministration
{
    Task<LinkedInPage<LinkedInAccount>> GetAccountsAsync(string? cursor, CancellationToken ct);
    Task<LinkedInPage<LinkedInWebhook>> GetWebhooksAsync(string? cursor, CancellationToken ct);
    Task<string> CreateWebhookAsync(LinkedInWebhookDefinition definition, CancellationToken ct);
}