using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using TalentScout.Data.ReadModels;

namespace TalentScout.Data.Repositories;

public sealed class EnrollmentRepository(AppDbContext db) : IEnrollmentRepository
{
    public async Task<Enrollment?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Enrollments.FindAsync([id], ct);
    }

    public Task<Enrollment?> FindForCandidateAsync(Guid campaignId, Guid candidateId, CancellationToken ct)
    {
        return db.Enrollments.SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.CandidateId == candidateId, ct);
    }

    public Task<Enrollment?> GetDetailsAsync(Guid id, CancellationToken ct)
    {
        return db.Enrollments.Include(x => x.Campaign).Include(x => x.Candidate)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
    {
        return db.Enrollments.AnyAsync(x => x.Id == id, ct);
    }

    public Task<List<CampaignCandidateRecord>> GetCampaignPageAsync(Guid campaignId, CandidateStatus? status, Guid? searchBatchId, OutreachApprovalStatus? approvalStatus, InvitationApprovalStatus? invitationApprovalStatus, int skip, int take, CancellationToken ct)
    {
        return db.Enrollments.AsNoTracking().Where(x => x.CampaignId == campaignId && (status == null || x.Status == status) &&
                                                        (searchBatchId == null || x.SearchBatchId == searchBatchId) && (approvalStatus == null || x.ApprovalStatus == approvalStatus) &&
                                                        (invitationApprovalStatus == null || x.InvitationApprovalStatus == invitationApprovalStatus))
            .OrderByDescending(x => x.FitScore).ThenBy(x => x.Id).Skip(skip).Take(take)
            .Select(x => new CampaignCandidateRecord(x.Id, x.CandidateId, x.Candidate.Name, x.Status, x.FitScore, x.Assessment, x.HasSentInitialMessage, x.HasSentInvitation, x.SearchBatchId, x.ApprovalStatus, x.ApprovedAt, x.OutreachStartedAt, x.InvitationApprovalStatus, x.InvitationApprovedAt)).ToListAsync(ct);
    }

    public Task<List<Guid>> GetPendingIdsAsync(int limit, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return db.Enrollments.AsNoTracking().Where(x => (x.NextContactAt == null || x.NextContactAt <= now) && x.ApprovalStatus != OutreachApprovalStatus.Excluded &&
                                                        (((x.Campaign.Status == CampaignStatus.Draft || x.Campaign.Status == CampaignStatus.Active) && x.Status == CandidateStatus.Found) ||
                                                         ((x.Campaign.Status == CampaignStatus.Active || (x.Campaign.Status == CampaignStatus.Draft && x.Campaign.AutoApproveCandidates)) &&
                                                          x.Status == CandidateStatus.Screened &&
                                                          (x.Campaign.AutoApproveCandidates || (x.ApprovalStatus == OutreachApprovalStatus.Approved && x.OutreachStartedAt != null))) ||
                                                         (x.Campaign.Status == CampaignStatus.Active &&
                                                          ((x.Status == CandidateStatus.Interested && x.InvitationApprovalStatus != InvitationApprovalStatus.Rejected &&
                                                            (x.Campaign.AutoApproveInterviewInvitations || x.InvitationApprovalStatus == InvitationApprovalStatus.Approved)) ||
                                                           db.Messages.Any(m => m.EnrollmentId == x.Id && m.Kind == MessageKind.Reply && !m.Analyzed)))))
            .OrderBy(x => x.NextContactAt ?? x.CreatedAt).ThenBy(x => x.Id).Take(limit).Select(x => x.Id).ToListAsync(ct);
    }

    public async Task ScheduleNextAttemptAsync(Guid id, DateTimeOffset nextAttempt, CancellationToken ct)
    {
        await db.Enrollments.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextContactAt, x => x.NextContactAt > nextAttempt ? x.NextContactAt : nextAttempt), ct);
    }

    public Task ReloadAsync(Enrollment enrollment, CancellationToken ct)
    {
        return db.Entry(enrollment).ReloadAsync(ct);
    }

    public Task<List<Enrollment>> GetSelectedAsync(Guid campaignId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        return db.Enrollments.Where(x => x.CampaignId == campaignId && ids.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<bool> TryAddAsync(Enrollment enrollment, CancellationToken ct)
    {
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"
            INSERT INTO ""Enrollments"" (""Id"", ""CampaignId"", ""CandidateId"", ""SearchBatchId"", ""Status"",
                ""ApprovalStatus"", ""ApprovedAt"", ""OutreachStartedAt"", ""InvitationApprovalStatus"", ""InvitationApprovedAt"", ""FitScore"", ""Assessment"",
                ""HasSentInitialMessage"", ""HasSentInvitation"", ""NextContactAt"", ""CreatedAt"", ""Version"")
            VALUES ({enrollment.Id}, {enrollment.CampaignId}, {enrollment.CandidateId}, {enrollment.SearchBatchId}, {(int)enrollment.Status},
                {(int)enrollment.ApprovalStatus}, {enrollment.ApprovedAt}, {enrollment.OutreachStartedAt}, {(int)enrollment.InvitationApprovalStatus}, {enrollment.InvitationApprovedAt}, {enrollment.FitScore}, {enrollment.Assessment},
                {enrollment.HasSentInitialMessage}, {enrollment.HasSentInvitation}, {enrollment.NextContactAt}, {enrollment.CreatedAt}, {enrollment.Version})
            ON CONFLICT (""CampaignId"", ""CandidateId"") DO NOTHING",
            ct);

        return inserted == 1;
    }

    public void Add(Enrollment enrollment)
    {
        db.Enrollments.Add(enrollment);
    }
}