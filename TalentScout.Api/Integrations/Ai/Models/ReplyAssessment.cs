using TalentScout.Data.Entities;

namespace TalentScout.Api.Integrations.Ai.Models;

public sealed record ReplyAssessment(Interest Interest, decimal Confidence, string Reason);