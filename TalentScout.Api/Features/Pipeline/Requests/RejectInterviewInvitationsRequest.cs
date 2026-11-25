namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed record RejectInterviewInvitationsRequest(IReadOnlyList<Guid> EnrollmentIds);
