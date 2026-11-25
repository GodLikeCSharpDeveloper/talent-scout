namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record ProfileSearchResult(IReadOnlyList<CandidateProfile> Profiles, string? NextCursor);