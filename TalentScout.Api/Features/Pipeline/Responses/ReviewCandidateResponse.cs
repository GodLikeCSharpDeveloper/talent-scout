using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record ReviewCandidateResponse(Guid Id, CandidateStatus Status);