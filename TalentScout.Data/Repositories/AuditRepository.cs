using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class AuditRepository(AppDbContext db) : IAuditRepository
{
    public void Add(Guid campaignId, Guid? enrollmentId, string action, string details = "")
    {
        db.AuditEvents.Add(new AuditEvent { CampaignId = campaignId, EnrollmentId = enrollmentId, Action = action, Details = details });
    }

    public Task<List<AuditEvent>> GetPageAsync(Guid campaignId, int skip, int take, CancellationToken ct)
    {
        return db.AuditEvents.AsNoTracking().Where(x => x.CampaignId == campaignId).OrderByDescending(x => x.OccurredAt)
            .ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }
}