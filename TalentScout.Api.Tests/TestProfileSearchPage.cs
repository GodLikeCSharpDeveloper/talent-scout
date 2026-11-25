using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Tests;

public sealed record TestProfileSearchPage(IReadOnlyList<CandidateProfile> Profiles, string? NextCursor);