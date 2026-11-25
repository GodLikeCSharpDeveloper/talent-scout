using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed record ReviewCandidateRequest(Interest Decision, string Reason);