using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInSearchParameters
{
    Task<IReadOnlyList<LinkedInSearchLocation>> FindLocationsAsync(string query, CancellationToken ct);
}