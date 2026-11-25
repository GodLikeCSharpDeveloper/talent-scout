using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class SearchBatchRepository(AppDbContext db) : ISearchBatchRepository
{
    public void Add(CandidateSearchBatch batch)
    {
        db.SearchBatches.Add(batch);
    }

    public async Task<CandidateSearchBatch?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.SearchBatches.FindAsync([id], ct);
    }

    public Task<int> CountEnrollmentsAsync(Guid id, CancellationToken ct)
    {
        return db.Enrollments.CountAsync(x => x.SearchBatchId == id, ct);
    }

    public Task<List<CandidateSearchBatch>> GetPageAsync(Guid campaignId, int skip, int take, CancellationToken ct)
    {
        return db.SearchBatches.AsNoTracking().Where(x => x.CampaignId == campaignId).OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }
}