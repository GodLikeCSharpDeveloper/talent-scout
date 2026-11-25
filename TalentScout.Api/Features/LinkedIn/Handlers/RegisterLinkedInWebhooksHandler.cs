using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class RegisterLinkedInWebhooksHandler(
    ILinkedInAdministration administration,
    IOptions<LinkedInOptions> options,
    IValidator<RegisterLinkedInWebhooksRequest> validator)
{
    public async Task<Result<LinkedInWebhookRegistration>> HandleAsync(CancellationToken ct)
    {
        var settings = options.Value;

        if (!settings.IsConfigured)
            return Result<LinkedInWebhookRegistration>.Unavailable("LinkedIn provider is not configured.");

        var validation = await validator.ValidateAsync(new RegisterLinkedInWebhooksRequest(settings.WebhookUrl), ct);

        if (!validation.IsValid)
            return Result<LinkedInWebhookRegistration>.Invalid(validation.AsErrors());

        var existing = await GetExistingAsync(ct);
        var messaging = await EnsureRegisteredAsync(LinkedInWebhookKind.Messages, existing, ct);
        var relations = await EnsureRegisteredAsync(LinkedInWebhookKind.Connections, existing, ct);

        return Result<LinkedInWebhookRegistration>.Success(
            new LinkedInWebhookRegistration(
                messaging.Id,
                settings.WebhookUrl,
                messaging.Existing && relations.Existing,
                relations.Id));
    }

    private async Task<List<LinkedInWebhook>> GetExistingAsync(CancellationToken ct)
    {
        var result = new List<LinkedInWebhook>();
        var visited = new HashSet<string>();
        string? cursor = null;
        do
        {
            var page = await administration.GetWebhooksAsync(cursor, ct);
            result.AddRange(page.Items);
            cursor = page.NextCursor;

            if (!string.IsNullOrEmpty(cursor) && !visited.Add(cursor))
                throw new ApiException(502, "LinkedIn repeated a webhook-list cursor.");
        } while (!string.IsNullOrEmpty(cursor));

        return result;
    }

    private async Task<(string Id, bool Existing)> EnsureRegisteredAsync(
        LinkedInWebhookKind kind,
        IReadOnlyList<LinkedInWebhook> existing,
        CancellationToken ct)
    {
        var settings = options.Value;
        var name = $"TalentScout/{settings.AccountId}/{kind}";
        var registered = existing.FirstOrDefault(x => x.Name == name && x.Url == settings.WebhookUrl);

        if (registered is not null)
            return (registered.Id, true);

        var id = await administration.CreateWebhookAsync(new LinkedInWebhookDefinition(name, settings.WebhookUrl, kind, settings.WebhookSecret), ct);

        return (id, false);
    }
}