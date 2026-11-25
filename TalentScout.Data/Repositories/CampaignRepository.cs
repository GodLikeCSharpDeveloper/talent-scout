using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class CampaignRepository(AppDbContext db) : ICampaignRepository
{
    public async Task<Campaign?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Campaigns.FindAsync([id], ct);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
    {
        return db.Campaigns.AnyAsync(x => x.Id == id, ct);
    }

    public Task<List<Campaign>> GetPageAsync(int skip, int take, CancellationToken ct)
    {
        return db.Campaigns.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task ReloadAsync(Campaign campaign, CancellationToken ct)
    {
        return db.Entry(campaign).ReloadAsync(ct);
    }

    public void Add(Campaign campaign)
    {
        db.Campaigns.Add(campaign);
    }
}