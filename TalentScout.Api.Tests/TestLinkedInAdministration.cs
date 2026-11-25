using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Tests;

public sealed class TestLinkedInAdministration : ILinkedInAdministration
{
    public List<LinkedInWebhook> Webhooks { get; } = [];
    public List<LinkedInWebhookDefinition> Created { get; } = [];
    public int ListCalls { get; private set; }
    public int PageSize { get; set; } = 100;
    public bool RepeatCursor { get; set; }
    public bool BlockRead { get; set; }
    public LinkedInWebhookKind? FailNextCreation { get; set; }

    public Task<LinkedInPage<LinkedInAccount>> GetAccountsAsync(string? cursor, CancellationToken ct)
    {
        return Task.FromResult(new LinkedInPage<LinkedInAccount>([], null));
    }

    public async Task<LinkedInPage<LinkedInWebhook>> GetWebhooksAsync(string? cursor, CancellationToken ct)
    {
        ListCalls++;
        if (BlockRead)
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        ct.ThrowIfCancellationRequested();

        if (RepeatCursor)
            return new LinkedInPage<LinkedInWebhook>([], "same");

        var offset = cursor is null ? 0 : int.Parse(cursor);
        var items = Webhooks.Skip(offset).Take(PageSize).ToList();

        return new LinkedInPage<LinkedInWebhook>(items, offset + items.Count < Webhooks.Count ? (offset + items.Count).ToString() : null);
    }

    public Task<string> CreateWebhookAsync(LinkedInWebhookDefinition definition, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailNextCreation == definition.Kind)
        {
            FailNextCreation = null;

            throw new HttpRequestException("Simulated webhook registration failure.");
        }

        var id = Guid.NewGuid().ToString();
        Created.Add(definition);
        Webhooks.Add(new LinkedInWebhook(id, definition.Name, definition.Url));

        return Task.FromResult(id);
    }
}