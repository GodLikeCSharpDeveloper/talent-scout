using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Disabled;

public sealed class DisabledLinkedInGateway : ILinkedInGateway, ILinkedInContacts, ILinkedInProfiles, ILinkedInConversations, ILinkedInSearchParameters, ILinkedInSynchronization
{
    public Task<LinkedInContactStatus> GetStatusAsync(string recipientId, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<string> SendInvitationAsync(string recipientId, string? note, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<LinkedInChat> GetChatAsync(string chatId, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<ProfileSearchResult> SearchAsync(ProfileSearch search, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<CandidateProfile> GetProfileAsync(string identifier, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<IReadOnlyList<LinkedInSearchLocation>> FindLocationsAsync(string query, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<LinkedInSyncSnapshot> ReadAsync(string recipientId, string? chatId, CancellationToken ct)
    {
        throw Unavailable();
    }

    private static ApiException Unavailable()
    {
        return new ApiException(503, "LinkedIn provider is not configured. Register an ILinkedInGateway adapter.");
    }
}