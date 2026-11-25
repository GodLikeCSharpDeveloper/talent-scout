using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn;

public sealed class UnipileWebhookWorker(IServiceScopeFactory scopes, IOptions<LinkedInOptions> options, ILogger<UnipileWebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var ids = await scope.ServiceProvider.GetRequiredService<ILinkedInWebhookRepository>().GetPendingIdsAsync(50, stoppingToken);
                foreach (var id in ids)
                {
                    using var itemScope = scopes.CreateScope();
                    await itemScope.ServiceProvider.GetRequiredService<ProcessUnipileWebhookHandler>().HandleAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Unipile inbox iteration failed: {ExceptionType}", exception.GetType().Name);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                return;
        }
    }
}