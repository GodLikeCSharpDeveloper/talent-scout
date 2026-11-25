namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record ProfileSearchResult(IReadOnlyList<LinkedInProfileReference> Profiles, string? NextCursor);