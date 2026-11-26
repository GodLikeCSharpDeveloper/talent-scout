using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline;

public sealed class MessageSendingWorker(IServiceScopeFactory scopes, PipelineExecutionService execution, IOptions<PipelineOptions> options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct)
    {
        return execution.RunAsync(nameof(MessageSendingWorker), SendBatchAsync, ct);
    }

    private async Task SendBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
        var ids = await repository.GetPendingMessageIdsAsync(options.Value.BatchSize, ct);
        foreach (var id in ids)
            await execution.ExecuteAsync<SendPipelineMessagesHandler>(id, static (handler, id, token) => handler.HandleAsync(id, token), ct);
    }
}