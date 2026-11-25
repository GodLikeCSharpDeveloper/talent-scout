using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Features.Candidates.Models;

public sealed record CandidateSearchCursor(
    string AccountId,
    string Query,
    string Location,
    IReadOnlyList<string> LocationIds,
    string? ProviderCursor,
    IReadOnlyList<LinkedInProfileReference> RemainingProfiles,
    DateTimeOffset ExpiresAt);