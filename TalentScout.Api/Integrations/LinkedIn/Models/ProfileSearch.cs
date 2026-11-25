namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record ProfileSearch(string Query, IReadOnlyList<string> LocationIds, int Limit, string? Cursor);