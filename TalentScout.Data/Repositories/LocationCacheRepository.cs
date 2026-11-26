using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class LocationCacheRepository(AppDbContext db) : ILocationCacheRepository
{
    public Task<LinkedInLocationCache?> FindAsync(string id, CancellationToken ct)
    {
        return db.LinkedInLocationCaches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task StoreAsync(LinkedInLocationCache cache, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"LinkedInLocationCaches\" (\"Id\", \"ResultsJson\", \"UpdatedAt\") VALUES ({cache.Id}, {cache.ResultsJson}, {cache.UpdatedAt}) ON CONFLICT (\"Id\") DO UPDATE SET \"ResultsJson\" = EXCLUDED.\"ResultsJson\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"", ct);
    }
}