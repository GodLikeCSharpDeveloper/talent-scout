using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class MessageRepository(AppDbContext db) : IMessageRepository
{
    public async Task<Message?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Messages.FindAsync([id], ct);
    }

    public Task<Message?> FindByExternalIdAsync(string externalId, CancellationToken ct)
    {
        return db.Messages.SingleOrDefaultAsync(x => x.ExternalId == externalId, ct);
    }

    public Task<Message?> FindOutgoingAsync(Guid enrollmentId, MessageKind kind, CancellationToken ct)
    {
        return db.Messages.SingleOrDefaultAsync(x => x.EnrollmentId == enrollmentId && x.Kind == kind, ct);
    }

    public Task<Message?> GetNextUnanalyzedReplyAsync(Guid enrollmentId, CancellationToken ct)
    {
        return db.Messages.Where(x => x.EnrollmentId == enrollmentId && x.Kind == MessageKind.Reply && !x.Analyzed)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    }

    public Task<List<Message>> GetHistoryAsync(Guid enrollmentId, DateTimeOffset through, CancellationToken ct)
    {
        return db.Messages.AsNoTracking().Where(x => x.EnrollmentId == enrollmentId && x.CreatedAt <= through)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public Task<List<Message>> GetPageAsync(Guid enrollmentId, int skip, int take, CancellationToken ct)
    {
        return db.Messages.AsNoTracking().Where(x => x.EnrollmentId == enrollmentId).OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public void Add(Message message)
    {
        db.Messages.Add(message);
    }

    public Task ReloadAsync(Message message, CancellationToken ct)
    {
        return db.Entry(message).ReloadAsync(ct);
    }

    public async Task MarkDeliveryUnknownAsync(Guid messageId, CancellationToken ct)
    {
        await db.Messages.Where(x => x.Id == messageId && x.Delivery == DeliveryStatus.Sending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Delivery, DeliveryStatus.Unknown).SetProperty(x => x.Version, Guid.NewGuid()), ct);
    }

    public async Task<bool> TryClaimDeliveryAsync(Enrollment enrollment, Message message, DateTimeOffset now, int intervalSeconds, int dailyLimit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var acquired = await db.Campaigns.Where(x => x.Id == enrollment.CampaignId &&
                                                     (x.Status == CampaignStatus.Active ||
                                                      (x.Status == CampaignStatus.Draft && x.AutoApproveCandidates)) &&
                                                     (x.NextOutboundAt == null || x.NextOutboundAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextOutboundAt, now.AddSeconds(intervalSeconds)), ct);

        if (acquired == 0)
            return false;

        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var attempts = await db.Messages.CountAsync(
            x => x.AttemptedAt >= dayStart &&
                 db.Enrollments.Any(e => e.Id == x.EnrollmentId && e.CampaignId == enrollment.CampaignId),
            ct);

        if (attempts >= dailyLimit)
            return false;

        // The campaign row is locked by the update above. Re-read its current permission before claiming a send.
        await db.Entry(enrollment.Campaign).ReloadAsync(ct);
        await db.Entry(enrollment).ReloadAsync(ct);

        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return false;
        if (!enrollment.Campaign.AutoApproveCandidates &&
            (enrollment.ApprovalStatus != OutreachApprovalStatus.Approved || enrollment.OutreachStartedAt is null))
            return false;

        var canSend = message.Kind switch
        {
            MessageKind.Initial => enrollment.Status == CandidateStatus.Screened && !enrollment.HasSentInitialMessage,
            MessageKind.Invitation => enrollment.Status == CandidateStatus.Interested && !enrollment.HasSentInvitation,
            _ => false
        };

        if (!canSend)
            return false;
        if (await db.Messages.AnyAsync(x => x.EnrollmentId == enrollment.Id && x.Kind == MessageKind.Reply && !x.Analyzed, ct))
            return false;
        if (await db.Interviews.AnyAsync(x => x.EnrollmentId == enrollment.Id, ct))
            return false;

        var claimed = await db.Messages.Where(x => x.Id == message.Id && x.Delivery == DeliveryStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Delivery, DeliveryStatus.Sending).SetProperty(x => x.AttemptedAt, now).SetProperty(x => x.Version, Guid.NewGuid()), ct);

        if (claimed == 0)
            return false;

        if (enrollment.Campaign.Status == CampaignStatus.Draft)
        {
            await db.Campaigns.Where(x => x.Id == enrollment.CampaignId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, CampaignStatus.Active)
                    .SetProperty(x => x.Version, Guid.NewGuid()), ct);
            await db.Entry(enrollment.Campaign).ReloadAsync(ct);
        }

        if (enrollment.Campaign.AutoApproveCandidates && enrollment.ApprovalStatus != OutreachApprovalStatus.Approved)
        {
            // Automatic permission is distinct from manual approval and is checked again on every send.
            await db.Enrollments.Where(x => x.Id == enrollment.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ApprovalStatus, OutreachApprovalStatus.Automatic)
                    .SetProperty(x => x.ApprovedAt, x => x.ApprovedAt ?? now)
                    .SetProperty(x => x.OutreachStartedAt, x => x.OutreachStartedAt ?? now)
                    .SetProperty(x => x.Version, Guid.NewGuid()), ct);
            await db.Entry(enrollment).ReloadAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return true;
    }
}