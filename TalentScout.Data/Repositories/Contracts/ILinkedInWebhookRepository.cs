using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ILinkedInWebhookRepository
{
    Task<bool> TryAddAsync(LinkedInWebhookEvent item, CancellationToken ct);
    Task<List<Guid>> GetPendingIdsAsync(int limit, CancellationToken ct);
    Task<LinkedInWebhookEvent?> TryClaimAsync(Guid id, CancellationToken ct);
    Task DeferAsync(Guid id, string error, CancellationToken ct);
    Task<List<LinkedInWebhookEvent>> GetPageAsync(int skip, int take, CancellationToken ct);
}