namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed record UnipileWebhookCreateRequest(
    string Name,
    string RequestUrl,
    string Source,
    IReadOnlyList<string> AccountIds,
    IReadOnlyList<string> Events,
    IReadOnlyList<UnipileWebhookHeader> Headers,
    string Format = "json",
    bool Enabled = true);