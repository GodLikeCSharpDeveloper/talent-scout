using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record EnrollmentResponse(Guid Id, Guid CampaignId, Guid CandidateId, CandidateStatus Status);