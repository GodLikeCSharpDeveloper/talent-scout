namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record ExcludeCandidatesResponse(IReadOnlyList<Guid> ExcludedEnrollmentIds);