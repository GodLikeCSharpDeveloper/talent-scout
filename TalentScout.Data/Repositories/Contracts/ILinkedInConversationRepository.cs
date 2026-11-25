using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ILinkedInConversationRepository
{
    Task<LinkedInConversation?> FindForEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<LinkedInConversation?> FindByChatAsync(string accountId, string chatId, CancellationToken ct);
    Task<LinkedInConversation?> FindByRecipientAsync(string accountId, string recipientId, CancellationToken ct);
    Task<LinkedInConversation> ReserveAsync(Guid enrollmentId, string accountId, string recipientId, CancellationToken ct);
    Task<bool> TryClaimInvitationAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task CompleteInvitationAsync(Guid id, string? invitationId, DeliveryStatus delivery, CancellationToken ct);
}