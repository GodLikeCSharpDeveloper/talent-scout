using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ILinkedInConversationRepository
{
    Task<LinkedInConversation?> FindForEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<LinkedInConversation?> FindByChatAsync(string accountId, string chatId, CancellationToken ct);
    Task<LinkedInConversation?> FindByRecipientAsync(string accountId, string recipientId, CancellationToken ct);
    Task<LinkedInConversation> ReserveAsync(Guid enrollmentId, string accountId, string recipientId, CancellationToken ct);
    Task<bool> TryClaimInvitationAsync(Guid id, Enrollment enrollment, DateTimeOffset now, int intervalSeconds, int dailyLimit, CancellationToken ct);
    Task CompleteInvitationAsync(Guid id, string? invitationId, DeliveryStatus delivery, CancellationToken ct);
    Task<List<Guid>> GetSynchronizationIdsAsync(string accountId, int limit, CancellationToken ct);
    Task<bool> TryAcquireSynchronizationAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task ReleaseSynchronizationAsync(Guid id, DateTimeOffset leaseUntil, DateTimeOffset next, string error, CancellationToken ct);
    Task<bool> ResetInvitationForRetryAsync(Guid id, CancellationToken ct);
}