using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;

namespace TalentScout.Data.Repositories;

public sealed class LinkedInConversationRepository(AppDbContext db) : ILinkedInConversationRepository
{
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
            INSERT INTO ""LinkedInConversations"" (""Id"", ""EnrollmentId"", ""AccountId"", ""RecipientId"", ""CreatedAt"", ""Version"")
            VALUES ({id}, {enrollmentId}, {accountId}, {recipientId}, {now}, {version})
            ON CONFLICT DO NOTHING",
            ct);

        return await FindByRecipientAsync(accountId, recipientId, ct)
               ?? await FindForEnrollmentAsync(enrollmentId, ct)
               ?? throw new InvalidOperationException("Conversation reservation could not be read.");
    }

    public async Task<bool> TryClaimInvitationAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        return await db.LinkedInConversations.Where(x => x.Id == id && x.ConnectionRequestDelivery == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ConnectionRequestDelivery, DeliveryStatus.Sending)
                    .SetProperty(x => x.ConnectionRequestedAt, now).SetProperty(x => x.Version, Guid.NewGuid()),
                ct) == 1;
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