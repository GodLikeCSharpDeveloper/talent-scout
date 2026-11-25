using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record EnrollmentStatusResponse(
    Guid Id,
    Guid CampaignId,
    Guid CandidateId,
    CandidateStatus Status,
    decimal? FitScore,
    string? Assessment,
    bool HasSentInitialMessage,
    bool HasSentInvitation,
    Guid? SearchBatchId,
    OutreachApprovalStatus ApprovalStatus,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? OutreachStartedAt,
    InvitationApprovalStatus InvitationApprovalStatus,
    DateTimeOffset? InvitationApprovedAt);