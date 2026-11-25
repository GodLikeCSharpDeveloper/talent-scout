namespace TalentScout.Api.Integrations.Ai.Models;

public sealed record ProfileAssessment(bool MeetsRequiredCriteria, decimal Score, string Reason);