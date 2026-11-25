using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInProfiles
{
    Task<CandidateProfile> GetProfileAsync(string identifier, CancellationToken ct);
}