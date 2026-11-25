using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Candidates.Responses;

public sealed record SearchBatchResponse(
    Guid Id,
    int RequestedCount,
    int AddedCount,
    int SkippedCount,
    int ScannedCount,
    int PagesScanned,
    CandidateSearchBatchStatus Status,
    CandidateSearchStopReason? StopReason,
    string? NextCursor,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);