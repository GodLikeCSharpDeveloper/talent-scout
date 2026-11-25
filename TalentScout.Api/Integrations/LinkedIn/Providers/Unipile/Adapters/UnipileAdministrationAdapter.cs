using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileAdministrationAdapter(
    IUnipileAdministrationApi administration,
    IOptions<LinkedInOptions> options) : ILinkedInAdministration
{
    public async Task<LinkedInPage<LinkedInAccount>> GetAccountsAsync(string? cursor, CancellationToken ct)
    {
        var page = await administration.GetAccountsAsync(cursor, ct);

        if (page.Items is null)
            throw new ApiException(502, "Unipile returned an invalid account page.");

        return new LinkedInPage<LinkedInAccount>(
            page.Items.Where(x => x.Type == "LINKEDIN")
                .Select(x => new LinkedInAccount(x.Id, x.Name ?? "", x.Type!)).ToList(),
            page.Cursor);
    }

    public async Task<LinkedInPage<LinkedInWebhook>> GetWebhooksAsync(string? cursor, CancellationToken ct)
    {
        var page = await administration.GetWebhooksAsync(cursor, ct);

        if (page.Items is null)
            throw new ApiException(502, "Unipile returned an invalid webhook page.");

        return new LinkedInPage<LinkedInWebhook>(page.Items.Select(x => new LinkedInWebhook(x.Id, x.Name ?? "", x.RequestUrl ?? "")).ToList(), page.Cursor);
    }

    public async Task<string> CreateWebhookAsync(LinkedInWebhookDefinition definition, CancellationToken ct)
    {
        var (source, eventName) = definition.Kind switch
        {
            LinkedInWebhookKind.Messages => ("messaging", "message_received"),
            LinkedInWebhookKind.Connections => ("users", "new_relation"),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };
        var result = await administration.CreateWebhookAsync(
            new UnipileWebhookCreateRequest(
                definition.Name,
                definition.Url,
                source,
                [options.Value.AccountId],
                [eventName],
                [new UnipileWebhookHeader("X-Unipile-Secret", definition.Secret)]),
            ct);

        return result.WebhookId ?? result.Id ?? throw new ApiException(502, "Unipile did not return a webhook identifier.");
    }
}