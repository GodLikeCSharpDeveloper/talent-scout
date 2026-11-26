using System.Data;
using System.Linq.Expressions;
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

    public Task<List<CampaignCandidateRecord>> GetCampaignPageAsync(CampaignCandidateQuery query, int skip, int take, CancellationToken ct)
    {
        return SortCandidates(ReadCandidateRecords(FilterCandidates(query)), query).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<CampaignCandidatePage> GetCandidatePageAsync(CampaignCandidateQuery query, int page, int pageSize, CancellationToken ct)
    {
        // Count and rows must see the same snapshot while screening/search updates arrive.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var filtered = FilterCandidates(query);
        var total = await filtered.CountAsync(ct);
        var currentPage = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var items = await SortCandidates(ReadCandidateRecords(filtered), query).Skip((currentPage - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        await transaction.CommitAsync(ct);

        return new CampaignCandidatePage(items, total, currentPage, pageSize);
    }

    public Task<List<Guid>> GetPendingProfileIdsAsync(int limit, CancellationToken ct)
    {
        var profiles = ReadyForPipeline()
            .Where(x => x.Status == CandidateStatus.Found)
            .Where(x => x.Campaign.Status == CampaignStatus.Draft || x.Campaign.Status == CampaignStatus.Active);

        return TakePendingIdsAsync(profiles, limit, ct);
    }

    public Task<List<Guid>> GetPendingMessageIdsAsync(int limit, CancellationToken ct)
    {
        var ready = ReadyForPipeline()
            .Where(x => !db.Messages.Any(m => m.EnrollmentId == x.Id && m.Kind == MessageKind.Reply && !m.Analyzed));

        var firstContacts = ready
            .Where(x => x.Status == CandidateStatus.Screened)
            .Where(x => x.Campaign.Status == CampaignStatus.Active || (x.Campaign.Status == CampaignStatus.Draft && x.Campaign.AutoApproveCandidates))
            .Where(x => x.Campaign.AutoApproveCandidates || (x.ApprovalStatus == OutreachApprovalStatus.Approved && x.OutreachStartedAt != null));

        var interviewInvitations = ready
            .Where(x => x.Status == CandidateStatus.Interested && x.Campaign.Status == CampaignStatus.Active)
            .Where(x => x.InvitationApprovalStatus != InvitationApprovalStatus.Rejected)
            .Where(x => x.Campaign.AutoApproveInterviewInvitations || x.InvitationApprovalStatus == InvitationApprovalStatus.Approved);

        return TakePendingIdsAsync(firstContacts.Union(interviewInvitations), limit, ct);
    }

    public Task<List<Guid>> GetPendingReplyIdsAsync(int limit, CancellationToken ct)
    {
        // Incoming replies are independent of outreach delays, including waiting for a connection.
        var replies = db.Enrollments.AsNoTracking()
            .Where(x => x.ApprovalStatus != OutreachApprovalStatus.Excluded)
            .Where(x => x.Campaign.Status == CampaignStatus.Active && x.HasSentInitialMessage)
            .Where(x => db.Messages.Any(m => m.EnrollmentId == x.Id && m.Kind == MessageKind.Reply && !m.Analyzed));

        return TakePendingIdsAsync(replies, limit, ct);
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

    private IQueryable<Enrollment> FilterCandidates(CampaignCandidateQuery filter)
    {
        var query = db.Enrollments.AsNoTracking().Where(x => x.CampaignId == filter.CampaignId);
        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status);
        if (filter.SearchBatchId.HasValue)
            query = query.Where(x => x.SearchBatchId == filter.SearchBatchId);
        if (filter.ApprovalStatus.HasValue)
            query = query.Where(x => x.ApprovalStatus == filter.ApprovalStatus);
        if (filter.InvitationApprovalStatus.HasValue)
            query = query.Where(x => x.InvitationApprovalStatus == filter.InvitationApprovalStatus);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLowerInvariant();
            query = query.Where(x => x.Candidate.Name.ToLower().Contains(search));
        }

        if (filter.NeedsDecision)
        {
            query = query.Where(x => x.Status == CandidateStatus.NeedsReview ||
                                     (x.Status == CandidateStatus.Screened && !x.HasSentInitialMessage && x.ApprovalStatus == OutreachApprovalStatus.Pending && !x.Campaign.AutoApproveCandidates) ||
                                     (x.Status == CandidateStatus.Interested && x.HasSentInitialMessage && !x.HasSentInvitation && x.ApprovalStatus != OutreachApprovalStatus.Excluded &&
                                      x.InvitationApprovalStatus == InvitationApprovalStatus.Pending && !x.Campaign.AutoApproveInterviewInvitations));
        }

        return query;
    }

    private static IOrderedQueryable<CampaignCandidateRecord> SortCandidates(IQueryable<CampaignCandidateRecord> query, CampaignCandidateQuery sort)
    {
        var ordered = sort.SortBy switch
        {
            CandidateSortField.Name => sort.Descending ? query.OrderByDescending(x => x.Name.ToLower()) : query.OrderBy(x => x.Name.ToLower()),
            CandidateSortField.Status => sort.Descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            CandidateSortField.FirstContact => SortFirstContact(query, sort.Descending),
            CandidateSortField.LinkedInStatus => sort.Descending ? query.OrderByDescending(x => x.LinkedInStatus) : query.OrderBy(x => x.LinkedInStatus),
            // Missing assessments are always last, in either direction.
            _ => sort.Descending ? query.OrderBy(x => x.FitScore == null).ThenByDescending(x => x.FitScore) : query.OrderBy(x => x.FitScore == null).ThenBy(x => x.FitScore)
        };

        return ordered.ThenBy(x => x.Id);
    }

    private static IOrderedQueryable<CampaignCandidateRecord> SortFirstContact(IQueryable<CampaignCandidateRecord> query, bool descending)
    {
        Expression<Func<CampaignCandidateRecord, int>> stage = x => x.HasSentInitialMessage ? 3 :
            x.ApprovalStatus == OutreachApprovalStatus.Excluded ? 4 :
            x.ApprovalStatus == OutreachApprovalStatus.Approved ? 2 :
            x.ApprovalStatus == OutreachApprovalStatus.Automatic ? 1 : 0;

        return descending ? query.OrderByDescending(stage) : query.OrderBy(stage);
    }

    private IQueryable<CampaignCandidateRecord> ReadCandidateRecords(IQueryable<Enrollment> query)
    {
        return query.Select(x => new CampaignCandidateRecord
        {
            Id = x.Id,
            CandidateId = x.CandidateId,
            Name = x.Candidate.Name,
            Status = x.Status,
            FitScore = x.FitScore,
            Assessment = x.Assessment,
            HasSentInitialMessage = x.HasSentInitialMessage,
            HasSentInvitation = x.HasSentInvitation,
            SearchBatchId = x.SearchBatchId,
            ApprovalStatus = x.ApprovalStatus,
            ApprovedAt = x.ApprovedAt,
            OutreachStartedAt = x.OutreachStartedAt,
            InvitationApprovalStatus = x.InvitationApprovalStatus,
            InvitationApprovedAt = x.InvitationApprovedAt,
            NextContactAt = x.NextContactAt,
            LastFailedAttemptAt = db.AuditEvents.Where(a => a.EnrollmentId == x.Id &&
                                                            (a.Action == "ProcessingFailed" || a.Action == "LinkedInConnectionRequestUnknown" || a.Action == "DeliveryOutcomeUnknown"))
                .OrderByDescending(a => a.OccurredAt).Select(a => (DateTimeOffset?)a.OccurredAt).FirstOrDefault(),
            ProfileUrl = x.Candidate.ProfileUrl,
            ProviderChatId = x.LinkedInConversation == null ? null : x.LinkedInConversation.ProviderChatId,
            ConnectionRequestDelivery = x.LinkedInConversation == null ? null : x.LinkedInConversation.ConnectionRequestDelivery,
            ConnectionState = x.LinkedInConversation == null ? null : x.LinkedInConversation.ConnectionState,
            LastSynchronizedAt = x.LinkedInConversation == null ? null : x.LinkedInConversation.LastSynchronizedAt,
            SynchronizationError = x.LinkedInConversation == null ? null : x.LinkedInConversation.SynchronizationError,
            RetryRequiresConfirmation = (x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState != "Connected" && x.LinkedInConversation.ConnectionState != "Withdrawn" && x.LinkedInConversation.ConnectionState != "Declined" && x.LinkedInConversation.ConnectionState != "Inactive" && (x.LinkedInConversation.ConnectionRequestDelivery == DeliveryStatus.Unknown || x.LinkedInConversation.ConnectionRequestDelivery == DeliveryStatus.Sending)) ||
                                        db.Messages.Any(m => m.EnrollmentId == x.Id && (m.Delivery == DeliveryStatus.Unknown || m.Delivery == DeliveryStatus.Sending)),
            LinkedInStatus = (x.LinkedInConversation != null && x.LinkedInConversation.SynchronizationError != null) ||
                             db.Messages.Any(m => m.EnrollmentId == x.Id && m.Delivery == DeliveryStatus.Unknown) ||
                             (x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState != "Connected" &&
                              x.LinkedInConversation.ConnectionState != "Withdrawn" && x.LinkedInConversation.ConnectionState != "Declined" && x.LinkedInConversation.ConnectionState != "Inactive" &&
                              (x.LinkedInConversation.ConnectionState == "ActionRequired" || x.LinkedInConversation.ConnectionRequestDelivery == DeliveryStatus.Unknown))
                ? LinkedInOutreachStatus.Failed
                : x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState == "Withdrawn"
                    ? LinkedInOutreachStatus.Withdrawn
                    : x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState == "Declined"
                        ? LinkedInOutreachStatus.Declined
                        : x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState == "Inactive"
                            ? LinkedInOutreachStatus.Inactive
                            : db.Messages.Any(m => m.EnrollmentId == x.Id && m.Delivery == DeliveryStatus.Sending) ||
                              (x.LinkedInConversation != null && x.LinkedInConversation.ConnectionRequestDelivery == DeliveryStatus.Sending)
                                ? LinkedInOutreachStatus.Sending
                                : x.HasSentInitialMessage
                                    ? LinkedInOutreachStatus.Conversation
                                    : x.LinkedInConversation != null && x.LinkedInConversation.ConnectionState == "Connected"
                                        ? LinkedInOutreachStatus.Connected
                                        : x.LinkedInConversation != null && (x.LinkedInConversation.ConnectionState == "Pending" || x.LinkedInConversation.ConnectionRequestDelivery == DeliveryStatus.Sent)
                                            ? LinkedInOutreachStatus.Pending
                                            : LinkedInOutreachStatus.NotStarted
        });
    }

    private IQueryable<Enrollment> ReadyForPipeline()
    {
        var now = DateTimeOffset.UtcNow;

        return db.Enrollments.AsNoTracking()
            .Where(x => x.ApprovalStatus != OutreachApprovalStatus.Excluded)
            .Where(x => x.NextContactAt == null || x.NextContactAt <= now);
    }

    private static Task<List<Guid>> TakePendingIdsAsync(IQueryable<Enrollment> query, int limit, CancellationToken ct)
    {
        return query.OrderBy(x => x.NextContactAt ?? x.CreatedAt).ThenBy(x => x.Id).Take(limit).Select(x => x.Id).ToListAsync(ct);
    }
}