using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class LinkedInConversationRepository(AppDbContext db) : ILinkedInConversationRepository
{
    public Task<List<Guid>> GetSynchronizationIdsAsync(string accountId, int limit, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return db.LinkedInConversations.AsNoTracking().Where(x => x.AccountId == accountId &&
                                                                  (x.NextSynchronizationAt == null || x.NextSynchronizationAt <= now) &&
                                                                  (x.SynchronizationLeaseUntil == null || x.SynchronizationLeaseUntil <= now))
            .OrderBy(x => x.NextSynchronizationAt ?? x.CreatedAt).ThenBy(x => x.Id).Take(limit).Select(x => x.EnrollmentId).ToListAsync(ct);
    }

    public async Task<bool> TryAcquireSynchronizationAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var acquired = await db.LinkedInConversations.Where(x => x.Id == id &&
                                                                 (x.SynchronizationLeaseUntil == null || x.SynchronizationLeaseUntil <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SynchronizationLeaseUntil, now.AddMinutes(10)).SetProperty(x => x.Version, Guid.NewGuid()), ct);

        if (acquired == 0)
            return false;

        var tracked = db.LinkedInConversations.Local.FirstOrDefault(x => x.Id == id);
        if (tracked is not null)
            await db.Entry(tracked).ReloadAsync(ct);

        return true;
    }

    public Task ReleaseSynchronizationAsync(Guid id, DateTimeOffset leaseUntil, DateTimeOffset next, string error, CancellationToken ct)
    {
        return db.LinkedInConversations.Where(x => x.Id == id && x.SynchronizationLeaseUntil == leaseUntil)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.SynchronizationLeaseUntil, (DateTimeOffset?)null)
                    .SetProperty(x => x.NextSynchronizationAt, next).SetProperty(x => x.SynchronizationError, error).SetProperty(x => x.Version, Guid.NewGuid()),
                ct);
    }

    public async Task<bool> ResetInvitationForRetryAsync(Guid id, CancellationToken ct)
    {
        var updated = await db.LinkedInConversations.Where(x => x.Id == id && x.SynchronizationLeaseUntil == null &&
                                                                x.ConnectionState != "Declined" && x.ConnectionState != "Inactive" &&
                                                                (x.ConnectionState == "Withdrawn" || x.ConnectionRequestDelivery == DeliveryStatus.Unknown || x.ConnectionRequestDelivery == DeliveryStatus.Sending))
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ConnectionRequestDelivery, (DeliveryStatus?)null)
                    .SetProperty(x => x.ConnectionRequestedAt, (DateTimeOffset?)null).SetProperty(x => x.InvitationId, (string?)null)
                    .SetProperty(x => x.ConnectionState, "NotConnected").SetProperty(x => x.Version, Guid.NewGuid()),
                ct);
        var tracked = db.LinkedInConversations.Local.FirstOrDefault(x => x.Id == id);
        if (tracked is not null)
            await db.Entry(tracked).ReloadAsync(ct);

        return updated == 1;
    }

    public Task<LinkedInConversation?> FindForEnrollmentAsync(Guid enrollmentId, CancellationToken ct)
    {
        return db.LinkedInConversations.SingleOrDefaultAsync(x => x.EnrollmentId == enrollmentId, ct);
    }

    public Task<LinkedInConversation?> FindByChatAsync(string accountId, string chatId, CancellationToken ct)
    {
        return db.LinkedInConversations.SingleOrDefaultAsync(x => x.AccountId == accountId && x.ChatId == chatId, ct);
    }

    public Task<LinkedInConversation?> FindByRecipientAsync(string accountId, string recipientId, CancellationToken ct)
    {
        return db.LinkedInConversations.SingleOrDefaultAsync(x => x.AccountId == accountId && x.RecipientId == recipientId, ct);
    }

    public async Task<LinkedInConversation> ReserveAsync(Guid enrollmentId, string accountId, string recipientId, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"
            INSERT INTO ""LinkedInConversations"" (""Id"", ""EnrollmentId"", ""AccountId"", ""RecipientId"", ""CreatedAt"", ""Version"", ""ConnectionState"")
            VALUES ({id}, {enrollmentId}, {accountId}, {recipientId}, {now}, {version}, {"Unknown"})
            ON CONFLICT DO NOTHING",
            ct);

        return await FindByRecipientAsync(accountId, recipientId, ct)
               ?? await FindForEnrollmentAsync(enrollmentId, ct)
               ?? throw new InvalidOperationException("Conversation reservation could not be read.");
    }

    public async Task<bool> TryClaimInvitationAsync(Guid id, Enrollment enrollment, DateTimeOffset now, int intervalSeconds, int dailyLimit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var campaignClaimed = await db.Campaigns.Where(x => x.Id == enrollment.CampaignId &&
                                                            (x.Status == CampaignStatus.Active || (x.Status == CampaignStatus.Draft && x.AutoApproveCandidates)) &&
                                                            (x.NextOutboundAt == null || x.NextOutboundAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextOutboundAt, now.AddSeconds(intervalSeconds)), ct);

        if (campaignClaimed == 0)
            return false;

        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var attempts = await db.LinkedInConversations.CountAsync(
            x => x.ConnectionRequestedAt >= dayStart &&
                 db.Enrollments.Any(e => e.Id == x.EnrollmentId && e.CampaignId == enrollment.CampaignId),
            ct);

        if (attempts >= dailyLimit)
            return false;

        // Recheck approval under the campaign lock: pausing/rejecting during AI generation prevents sending.
        var enrollmentClaimed = await db.Enrollments.Where(x => x.Id == enrollment.Id && x.Version == enrollment.Version &&
                                                                x.Status == CandidateStatus.Screened && !x.HasSentInitialMessage && x.ApprovalStatus != OutreachApprovalStatus.Excluded &&
                                                                (x.Campaign.AutoApproveCandidates || (x.ApprovalStatus == OutreachApprovalStatus.Approved && x.OutreachStartedAt != null)))
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Version, Guid.NewGuid())
                    .SetProperty(x => x.OutreachStartedAt, x => x.OutreachStartedAt ?? now)
                    .SetProperty(x => x.ApprovalStatus, x => x.ApprovalStatus == OutreachApprovalStatus.Approved ? OutreachApprovalStatus.Approved : OutreachApprovalStatus.Automatic),
                ct);

        if (enrollmentClaimed == 0)
            return false;

        if (await db.LinkedInConversations.AnyAsync(x => x.Id == id && x.SynchronizationLeaseUntil > now, ct))
            return false;

        var claimed = await db.LinkedInConversations.Where(x => x.Id == id && x.ConnectionRequestDelivery == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ConnectionRequestDelivery, DeliveryStatus.Sending)
                    .SetProperty(x => x.ConnectionRequestedAt, now).SetProperty(x => x.Version, Guid.NewGuid()),
                ct);

        if (claimed == 0)
            return false;

        await transaction.CommitAsync(ct);
        await db.Entry(enrollment).ReloadAsync(ct);

        return true;
    }

    public async Task CompleteInvitationAsync(Guid id, string? invitationId, DeliveryStatus delivery, CancellationToken ct)
    {
        await db.LinkedInConversations.Where(x => x.Id == id && x.ConnectionRequestDelivery == DeliveryStatus.Sending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.InvitationId, invitationId)
                    .SetProperty(x => x.ConnectionRequestDelivery, delivery).SetProperty(x => x.Version, Guid.NewGuid()),
                ct);
        var tracked = db.LinkedInConversations.Local.FirstOrDefault(x => x.Id == id);
        if (tracked is not null)
            await db.Entry(tracked).ReloadAsync(ct);
    }
}