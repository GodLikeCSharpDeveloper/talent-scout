namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed record ApproveInterviewInvitationsRequest(IReadOnlyList<Guid> EnrollmentIds);