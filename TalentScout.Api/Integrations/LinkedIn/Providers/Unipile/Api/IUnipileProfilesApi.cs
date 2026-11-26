using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public interface IUnipileProfilesApi
{
    Task<UnipilePage<UnipileSentInvitation>> GetSentInvitationsAsync(string accountId, string? cursor, CancellationToken ct);
    Task<UnipileProfile> GetAsync(string accountId, string identifier, CancellationToken ct);
    Task<UnipileInvitationResponse> InviteAsync(string accountId, string recipientId, string? note, CancellationToken ct);
}