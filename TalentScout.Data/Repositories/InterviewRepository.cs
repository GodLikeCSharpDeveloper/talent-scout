using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class InterviewRepository(AppDbContext db) : IInterviewRepository
{
    public Task<Interview?> FindBookingAsync(string provider, string externalId, CancellationToken ct)
    {
        return db.Interviews.SingleOrDefaultAsync(x => x.Provider == provider && x.ExternalBookingId == externalId, ct);
    }

    public Task<bool> ExistsForEnrollmentAsync(Guid enrollmentId, CancellationToken ct)
    {
        return db.Interviews.AnyAsync(x => x.EnrollmentId == enrollmentId, ct);
    }

    public Task<List<Interview>> GetPageAsync(Guid? campaignId, int skip, int take, CancellationToken ct)
    {
        return db.Interviews.AsNoTracking().Where(x => campaignId == null || db.Enrollments.Any(e => e.Id == x.EnrollmentId && e.CampaignId == campaignId))
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public void Add(Interview interview)
    {
        db.Interviews.Add(interview);
    }
}