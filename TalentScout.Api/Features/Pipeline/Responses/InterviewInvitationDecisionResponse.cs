using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record InterviewInvitationDecisionResponse(IReadOnlyList<Guid> EnrollmentIds, InvitationApprovalStatus InvitationApprovalStatus);