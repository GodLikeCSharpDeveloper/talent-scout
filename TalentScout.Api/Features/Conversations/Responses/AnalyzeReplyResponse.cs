using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Conversations.Responses;

public sealed record AnalyzeReplyResponse(
    Guid EnrollmentId,
    Guid MessageId,
    CandidateStatus Status,
    Interest? Interest,
    decimal? Confidence,
    string? Analysis);