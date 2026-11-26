using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline;

public sealed class ReplyAnalysisWorker(IServiceScopeFactory scopes, PipelineExecutionService execution, IOptions<PipelineOptions> options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct)
    {
        return execution.RunAsync(nameof(ReplyAnalysisWorker), AnalyzeBatchAsync, ct);
    }

    private async Task AnalyzeBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
        var ids = await repository.GetPendingReplyIdsAsync(options.Value.BatchSize, ct);
        foreach (var id in ids)
            await execution.ExecuteAsync<AnalyzeReplyHandler>(id, static async (handler, id, token) => await handler.HandleAsync(id, token), ct);
    }
}