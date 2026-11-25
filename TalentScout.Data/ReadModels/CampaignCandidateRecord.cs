using TalentScout.Data.Entities;

namespace TalentScout.Data.ReadModels;

public sealed record CampaignCandidateRecord(
    Guid Id,
    Guid CandidateId,
    string Name,
    CandidateStatus Status,
    decimal? FitScore,
    string? Assessment,
    bool HasSentInitialMessage,
    bool HasSentInvitation,
    Guid? SearchBatchId,
    OutreachApprovalStatus ApprovalStatus,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? OutreachStartedAt);