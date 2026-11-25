using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class LinkedInWebhookRepository(AppDbContext db) : ILinkedInWebhookRepository
{
    public async Task<bool> TryAddAsync(LinkedInWebhookEvent x, CancellationToken ct)
    {
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $@"
            INSERT INTO ""LinkedInWebhookEvents"" (""Id"", ""EventType"", ""AccountId"", ""ExternalMessageId"", ""ChatId"", ""ProviderChatId"",
                ""SenderId"", ""Content"", ""HasAttachments"", ""OccurredAt"", ""ReceivedAt"", ""NextAttemptAt"", ""Attempts"")
            VALUES ({x.Id}, {x.EventType}, {x.AccountId}, {x.ExternalMessageId}, {x.ChatId}, {x.ProviderChatId},
                {x.SenderId}, {x.Content}, {x.HasAttachments}, {x.OccurredAt}, {x.ReceivedAt}, {x.NextAttemptAt}, 0)
            ON CONFLICT (""AccountId"", ""ExternalMessageId"") DO NOTHING",
            ct) == 1;
    }

    public Task<List<Guid>> GetPendingIdsAsync(int limit, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return db.LinkedInWebhookEvents.AsNoTracking().Where(x => x.ProcessedAt == null && x.NextAttemptAt <= now &&
                                                                  (x.LeaseUntil == null || x.LeaseUntil < now)).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.OccurredAt)
            .Take(limit).Select(x => x.Id).ToListAsync(ct);
    }

    public async Task<LinkedInWebhookEvent?> TryClaimAsync(Guid id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var claimed = await db.LinkedInWebhookEvents.Where(x => x.Id == id && x.ProcessedAt == null &&
                                                                x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now))
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LeaseUntil, now.AddMinutes(2))
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1),
                ct);

        return claimed == 0 ? null : await db.LinkedInWebhookEvents.SingleAsync(x => x.Id == id, ct);
    }

    public async Task DeferAsync(Guid id, string error, CancellationToken ct)
    {
        await db.LinkedInWebhookEvents.Where(x => x.Id == id && x.ProcessedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastError, error)
                    .SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow.AddMinutes(1))
                    .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null),
                ct);
    }

    public Task<List<LinkedInWebhookEvent>> GetPageAsync(int skip, int take, CancellationToken ct)
    {
        return db.LinkedInWebhookEvents.AsNoTracking().OrderByDescending(x => x.ReceivedAt).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
    }
}