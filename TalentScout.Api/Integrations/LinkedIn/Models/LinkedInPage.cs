namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record LinkedInPage<T>(IReadOnlyList<T> Items, string? NextCursor);