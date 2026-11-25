namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record ProfileSearch(string Query, string Location, int Limit, string? Cursor);