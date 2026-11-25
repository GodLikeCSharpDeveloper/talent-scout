using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ISearchBatchRepository
{
    void Add(CandidateSearchBatch batch);
    Task<CandidateSearchBatch?> FindAsync(Guid id, CancellationToken ct);
    Task<int> CountEnrollmentsAsync(Guid id, CancellationToken ct);
    Task<List<CandidateSearchBatch>> GetPageAsync(Guid campaignId, int skip, int take, CancellationToken ct);
}