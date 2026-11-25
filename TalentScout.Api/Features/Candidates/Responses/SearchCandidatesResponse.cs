using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Candidates.Responses;

public sealed record SearchCandidatesResponse(
    Guid SearchBatchId,
    IReadOnlyList<Guid> EnrollmentIds,
    int RequestedCount,
    int AddedCount,
    int SkippedCount,
    int ScannedCount,
    int PagesScanned,
    string? NextCursor,
    CandidateSearchStopReason StopReason);