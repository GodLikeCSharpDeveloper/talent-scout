using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileContactsAdapter(IUnipileProfilesApi profiles, IOptions<LinkedInOptions> options) : ILinkedInContacts
{
    public async Task<LinkedInContactStatus> GetStatusAsync(string recipientId, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(options.Value.AccountId, recipientId, ct);
        var connected = profile.IsRelationship || profile.NetworkDistance == "FIRST_DEGREE";

        return new LinkedInContactStatus(
            connected || options.Value.UseInMail,
            connected,
            profile.Invitation is { Type: "SENT", Status: "PENDING" });
    }

    public async Task<string> SendInvitationAsync(string recipientId, string? note, CancellationToken ct)
    {
        var invitation = await profiles.InviteAsync(options.Value.AccountId, recipientId, note, ct);

        return !string.IsNullOrWhiteSpace(invitation.InvitationId)
            ? invitation.InvitationId
            : throw new ApiException(502, "Unipile did not confirm the connection invitation.");
    }
}