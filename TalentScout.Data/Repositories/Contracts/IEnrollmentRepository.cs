using TalentScout.Data.Entities;
using TalentScout.Data.ReadModels;

namespace TalentScout.Data.Repositories;

public interface IEnrollmentRepository
{
    Task<Enrollment?> FindAsync(Guid id, CancellationToken ct);
    Task<Enrollment?> FindForCandidateAsync(Guid campaignId, Guid candidateId, CancellationToken ct);
    Task<Enrollment?> GetDetailsAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<List<CampaignCandidateRecord>> GetCampaignPageAsync(Guid campaignId, CandidateStatus? status, Guid? searchBatchId, OutreachApprovalStatus? approvalStatus, int skip, int take, CancellationToken ct);
    Task<List<Guid>> GetPendingIdsAsync(int limit, CancellationToken ct);
    Task ScheduleNextAttemptAsync(Guid id, DateTimeOffset nextAttempt, CancellationToken ct);
    Task ReloadAsync(Enrollment enrollment, CancellationToken ct);
    Task<bool> TryAddAsync(Enrollment enrollment, CancellationToken ct);
    Task<List<Enrollment>> GetSelectedAsync(Guid campaignId, IReadOnlyList<Guid> ids, CancellationToken ct);
    void Add(Enrollment enrollment);
}