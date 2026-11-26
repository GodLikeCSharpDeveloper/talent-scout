using Ardalis.Result;
using TalentScout.Data.Repositories;
using IResult = Ardalis.Result.IResult;

namespace TalentScout.Api.Features.Pipeline;

// Scheduling, fresh scopes and failure recovery are shared; workers own their queries and actions.
public sealed class PipelineExecutionService(
    IServiceScopeFactory scopes,
    IOptions<PipelineOptions> options,
    ILogger<PipelineExecutionService> logger)
{
    public async Task RunAsync(string worker, Func<CancellationToken, Task> runBatch, CancellationToken ct)
    {
        if (!options.Value.WorkerEnabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await runBatch(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError("{Worker} batch failed: {ExceptionType}", worker, exception.GetType().Name);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(ct))
                    return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public async Task ExecuteAsync<THandler>(Guid id, Func<THandler, Guid, CancellationToken, Task<IResult?>> action, CancellationToken ct)
        where THandler : notnull
    {
        string? failure = null;
        try
        {
            using var scope = scopes.CreateScope();
            var result = await action(scope.ServiceProvider.GetRequiredService<THandler>(), id, ct);
            if (result is not null && result.Status is not (ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent))
                failure = result.Status.ToString();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            failure = exception is ApiException ? exception.Message : exception.GetType().Name;
        }

        if (failure is not null)
            await RecordFailureAsync(id, typeof(THandler).Name, failure, ct);

        using var schedulingScope = scopes.CreateScope();
        await schedulingScope.ServiceProvider.GetRequiredService<IEnrollmentRepository>()
            .ScheduleNextAttemptAsync(id, DateTimeOffset.UtcNow.AddSeconds(options.Value.PollIntervalSeconds), ct);
    }

    private async Task RecordFailureAsync(Guid id, string handler, string reason, CancellationToken ct)
    {
        logger.LogWarning("{Handler} failed for {EnrollmentId}: {Reason}", handler, id, reason);
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var enrollment = await services.GetRequiredService<IEnrollmentRepository>().FindAsync(id, ct);

        if (enrollment is null)
            return;

        services.GetRequiredService<IAuditRepository>().Add(enrollment.CampaignId, id, "ProcessingFailed", $"{handler}: {reason}");
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }
}