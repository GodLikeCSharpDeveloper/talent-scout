using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface ICandidateRepository
{
    Task<Candidate?> FindAsync(Guid id, CancellationToken ct);
    Task<Candidate?> FindByExternalIdAsync(string externalId, CancellationToken ct);
    Task<Candidate?> FindByIdentityAsync(string externalId, string profileUrl, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<List<Candidate>> GetPageAsync(int skip, int take, CancellationToken ct);
    void Add(Candidate candidate);
}