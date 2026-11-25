using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ICampaignRepository
{
    Task<Campaign?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<List<Campaign>> GetPageAsync(int skip, int take, CancellationToken ct);
    Task ReloadAsync(Campaign campaign, CancellationToken ct);
    void Add(Campaign campaign);
}