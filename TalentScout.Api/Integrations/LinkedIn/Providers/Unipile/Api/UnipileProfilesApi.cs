using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public sealed class UnipileProfilesApi(IUnipileHttpClient http) : IUnipileProfilesApi
{
    public Task<UnipilePage<UnipileSentInvitation>> GetSentInvitationsAsync(string accountId, string? cursor, CancellationToken ct)
    {
        return http.GetAsync<UnipilePage<UnipileSentInvitation>>(
            $"users/invite/sent?account_id={Uri.EscapeDataString(accountId)}&limit=100" +
            (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor)),
            ct);
    }

    public Task<UnipileProfile> GetAsync(string accountId, string identifier, CancellationToken ct)
    {
        return http.GetAsync<UnipileProfile>(
            $"users/{Uri.EscapeDataString(identifier)}?account_id={Uri.EscapeDataString(accountId)}&linkedin_sections=experience",
            ct);
    }

    public Task<UnipileInvitationResponse> InviteAsync(string accountId, string recipientId, string? note, CancellationToken ct)
    {
        return http.PostAsync<UnipileInvitationResponse>(
            "users/invite",
            new { account_id = accountId, provider_id = recipientId, message = note },
            ct);
    }
}