using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Integrations.LinkedIn.Options;

namespace TalentScout.Api.Features.LinkedIn;

public sealed class LinkedInWebhookStartupService(
    IServiceScopeFactory scopes,
    IOptions<LinkedInOptions> options,
    ILogger<LinkedInWebhookStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return;

        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<RegisterLinkedInWebhooksHandler>().HandleAsync(ct);
        if (!result.IsSuccess)
        {
            var errors = result.ValidationErrors.Select(x => x.ErrorMessage).ToArray();

            throw new OptionsValidationException(
                nameof(LinkedInOptions),
                typeof(LinkedInOptions),
                errors.Length > 0 ? errors : ["LinkedIn webhook registration failed: " + result.Status]);
        }

        logger.LogInformation("LinkedIn message and connection webhooks are registered.");
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}