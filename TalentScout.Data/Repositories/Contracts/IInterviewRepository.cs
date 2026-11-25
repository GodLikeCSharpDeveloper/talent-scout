using TalentScout.Data.Entities;

namespace TalentScout.Data.Repositories;

public interface IInterviewRepository
{
    Task<Interview?> FindBookingAsync(string provider, string externalId, CancellationToken ct);
    Task<bool> ExistsForEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<List<Interview>> GetPageAsync(Guid? campaignId, int skip, int take, CancellationToken ct);
    void Add(Interview interview);
}