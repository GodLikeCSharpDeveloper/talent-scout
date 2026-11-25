using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Candidates.Responses;

public sealed record ScreenProfileResponse(Guid EnrollmentId, CandidateStatus Status, decimal? FitScore, string? Assessment, OutreachApprovalStatus ApprovalStatus, DateTimeOffset? ApprovedAt, DateTimeOffset? OutreachStartedAt);