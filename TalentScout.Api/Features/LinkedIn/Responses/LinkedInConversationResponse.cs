using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.LinkedIn.Responses;

public sealed record LinkedInConversationResponse(
    Guid EnrollmentId,
    string AccountId,
    string RecipientId,
    string? ChatId,
    string? ConversationUrl,
    DeliveryStatus? ConnectionRequestDelivery,
    string? InvitationId,
    DateTimeOffset? ConnectionConfirmedAt,
    string? ConnectionNote = null,
    string ConnectionState = "Unknown",
    DateTimeOffset? LastSynchronizedAt = null,
    string? SynchronizationError = null);