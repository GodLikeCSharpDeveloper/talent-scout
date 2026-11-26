using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ILocationCacheRepository
{
    Task<LinkedInLocationCache?> FindAsync(string id, CancellationToken ct);
    Task StoreAsync(LinkedInLocationCache cache, CancellationToken ct);
}