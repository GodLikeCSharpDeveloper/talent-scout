using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

/// <summary>Implement only through the selected provider's permitted API. Map stable provider IDs to these contracts.</summary>
public interface ILinkedInGateway
{
    Task<ProfileSearchResult> SearchAsync(ProfileSearch search, CancellationToken ct);

    // Adapters must forward IdempotencyKey when supported and must never silently retry ambiguous sends.
    Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct);
}