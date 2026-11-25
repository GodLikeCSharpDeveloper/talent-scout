using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Disabled;

public sealed class DisabledLinkedInGateway : ILinkedInGateway
{
    public bool IsConfigured => false;

    public Task<ProfileSearchResult> SearchAsync(ProfileSearch search, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        throw Unavailable();
    }

    private static ApiException Unavailable()
    {
        return new ApiException(503, "LinkedIn provider is not configured. Register an ILinkedInGateway adapter.");
    }
}