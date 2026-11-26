using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TalentScout.Api.Features.Realtime;

public sealed class WorkspaceChangeInterceptor(WorkspaceEvents events) : SaveChangesInterceptor
{
    private string[] changed = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        changed = eventData.Context?.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(e => e.Metadata.ClrType.Name).Distinct().ToArray() ?? [];

        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var entities = changed;
        changed = [];
        if (entities.Length > 0)
            await events.ChangedAsync(entities);

        return result;
    }
}