using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Conversations.Responses;

public sealed record SendMessageResponse(
    Guid EnrollmentId,
    Guid MessageId,
    MessageKind Kind,
    DeliveryStatus Delivery,
    CandidateStatus Status,
    string Content,
    DateTimeOffset? SentAt);