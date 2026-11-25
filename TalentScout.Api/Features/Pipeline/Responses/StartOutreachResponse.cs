namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record StartOutreachResponse(IReadOnlyList<Guid> StartedEnrollmentIds, IReadOnlyList<Guid> AlreadyStartedEnrollmentIds);