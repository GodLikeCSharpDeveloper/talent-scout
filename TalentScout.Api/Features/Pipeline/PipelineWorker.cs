using Ardalis.Result;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline;

public sealed class PipelineWorker(IServiceScopeFactory scopes, IOptions<PipelineOptions> options, ILogger<PipelineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var enrollments = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
                var ids = await enrollments.GetPendingIdsAsync(options.Value.BatchSize, stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var itemScope = scopes.CreateScope();
                        var coordinator = itemScope.ServiceProvider.GetRequiredService<PipelineCoordinator>();
                        var result = await coordinator.AdvanceAsync(id, stoppingToken);
                        if (result is not null && result.Status is not (ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent))
                        {
                            logger.LogWarning("Pipeline stage failed for {EnrollmentId}: {Status}", id, result.Status);
                            await RecordFailureAsync(id, result.Status.ToString(), stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogWarning("Processing failed for enrollment {EnrollmentId}: {ExceptionType}", id, exception.GetType().Name);
                        await RecordFailureAsync(id, exception.GetType().Name, stoppingToken);
                    }

                    await enrollments.ScheduleNextAttemptAsync(id, DateTimeOffset.UtcNow.AddSeconds(options.Value.PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("Pipeline iteration failed: {ExceptionType}", exception.GetType().Name);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                return;
        }
    }

    private async Task RecordFailureAsync(Guid id, string details, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var enrollment = await scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>().FindAsync(id, ct);

        if (enrollment is null)
            return;

        scope.ServiceProvider.GetRequiredService<IAuditRepository>().Add(enrollment.CampaignId, id, "ProcessingFailed", details);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }
}