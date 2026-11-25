using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInContacts
{
    Task<LinkedInContactStatus> GetStatusAsync(string recipientId, CancellationToken ct);
    Task<string> SendInvitationAsync(string recipientId, string? note, CancellationToken ct);
}