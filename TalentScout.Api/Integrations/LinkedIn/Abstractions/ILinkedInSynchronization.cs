using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInSynchronization
{
    Task<LinkedInSyncSnapshot> ReadAsync(string recipientId, string? chatId, CancellationToken ct);
}