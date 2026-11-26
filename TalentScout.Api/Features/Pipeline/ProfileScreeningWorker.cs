using TalentScout.Api.Features.Candidates.Handlers;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline;

public sealed class ProfileScreeningWorker(IServiceScopeFactory scopes, PipelineExecutionService execution, IOptions<PipelineOptions> options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct)
    {
        return execution.RunAsync(nameof(ProfileScreeningWorker), ScreenBatchAsync, ct);
    }

    private async Task ScreenBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
        var ids = await repository.GetPendingProfileIdsAsync(options.Value.BatchSize, ct);
        foreach (var id in ids)
            await execution.ExecuteAsync<ScreenProfileHandler>(id, static async (handler, id, token) => await handler.HandleAsync(id, token), ct);
    }
}