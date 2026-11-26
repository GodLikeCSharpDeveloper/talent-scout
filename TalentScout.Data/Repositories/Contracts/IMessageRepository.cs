using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface IMessageRepository
{
    Task<Message?> FindAsync(Guid id, CancellationToken ct);
    Task<Message?> FindByExternalIdAsync(string externalId, CancellationToken ct);
    Task<Message?> FindOutgoingAsync(Guid enrollmentId, MessageKind kind, CancellationToken ct);
    Task<Message?> GetNextUnanalyzedReplyAsync(Guid enrollmentId, CancellationToken ct);
    Task<List<Message>> GetHistoryAsync(Guid enrollmentId, DateTimeOffset through, CancellationToken ct);
    Task<List<Message>> GetPageAsync(Guid enrollmentId, int skip, int take, CancellationToken ct);
    Task<bool> TryClaimDeliveryAsync(Enrollment enrollment, Message message, DateTimeOffset now, int intervalSeconds, int dailyLimit, CancellationToken ct);
    Task ReloadAsync(Message message, CancellationToken ct);
    Task MarkDeliveryUnknownAsync(Guid messageId, CancellationToken ct);
    Task<List<Message>> GetForSynchronizationAsync(Guid enrollmentId, CancellationToken ct);
    Task<HashSet<string>> GetExistingExternalIdsAsync(IReadOnlyCollection<string> externalIds, CancellationToken ct);
    Task<bool> ResetForRetryAsync(Guid messageId, CancellationToken ct);
    void Add(Message message);
}