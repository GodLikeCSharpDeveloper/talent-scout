namespace TalentScout.Api.Integrations.Ai.Models;

public sealed record Vacancy(string Title, string Description, string Requirements, string SystemPrompt = "");