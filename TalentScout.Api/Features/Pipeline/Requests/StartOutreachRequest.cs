namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed record StartOutreachRequest(IReadOnlyList<Guid> EnrollmentIds);