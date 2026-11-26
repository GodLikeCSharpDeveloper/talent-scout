namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record LinkedInSyncMessage(
    string ExternalId,
    string Text,
    DateTimeOffset Timestamp,
    bool IsOutgoing,
    bool IsDeleted = false,
    bool IsEdited = false,
    bool HasAttachments = false);

public sealed record LinkedInSyncSnapshot(
    LinkedInContactStatus Contact,
    LinkedInChat? Chat,
    IReadOnlyList<LinkedInSyncMessage> Messages,
    string? PendingInvitationId = null,
    string? InvitationNote = null);