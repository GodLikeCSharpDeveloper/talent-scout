using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline;

public sealed class LinkedInSynchronizationWorker(
    IServiceScopeFactory scopes,
    IOptions<PipelineOptions> options,
    IOptions<LinkedInOptions> linkedIn,
    ILogger<LinkedInSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.WorkerEnabled || !linkedIn.Value.IsConfigured)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.SynchronizationIntervalMinutes));
        do
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var ids = await scope.ServiceProvider.GetRequiredService<ILinkedInConversationRepository>()
                        .GetSynchronizationIdsAsync(linkedIn.Value.AccountId, options.Value.BatchSize, ct);

                    if (ids.Count == 0)
                        break;

                    foreach (var id in ids)
                    {
                        using var itemScope = scopes.CreateScope();
                        try
                        {
                            await itemScope.ServiceProvider.GetRequiredService<SynchronizeLinkedInHandler>().HandleAsync(id, ct);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception exception)
                        {
                            logger.LogWarning("LinkedIn synchronization failed for {EnrollmentId}: {ExceptionType}", id, exception.GetType().Name);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("LinkedIn synchronization batch failed: {ExceptionType}", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}