using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface IAuditRepository
{
    void Add(Guid campaignId, Guid? enrollmentId, string action, string details = "");
    Task<List<AuditEvent>> GetPageAsync(Guid campaignId, int skip, int take, CancellationToken ct);
}