namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed record ExcludeCandidatesRequest(IReadOnlyList<Guid> EnrollmentIds);