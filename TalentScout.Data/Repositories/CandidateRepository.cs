using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class CandidateRepository(AppDbContext db) : ICandidateRepository
{
    public async Task<Candidate?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Candidates.FindAsync([id], ct);
    }

    public Task<Candidate?> FindByExternalIdAsync(string externalId, CancellationToken ct)
    {
        return db.Candidates.SingleOrDefaultAsync(x => x.ExternalId == externalId, ct);
    }

    public Task<Candidate?> FindByIdentityAsync(string externalId, string profileUrl, CancellationToken ct)
    {
        return db.Candidates.SingleOrDefaultAsync(x => x.ExternalId == externalId || x.ProfileUrl == profileUrl, ct);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
    {
        return db.Candidates.AnyAsync(x => x.Id == id, ct);
    }

    public Task<List<Candidate>> GetPageAsync(int skip, int take, CancellationToken ct)
    {
        return db.Candidates.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public void Add(Candidate candidate)
    {
        db.Candidates.Add(candidate);
    }
}