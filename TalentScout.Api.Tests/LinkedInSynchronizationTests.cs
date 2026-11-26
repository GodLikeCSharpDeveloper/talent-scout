using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using TalentScout.Data.Repositories;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Theory]
    [InlineData(ConnectionInvitationStatus.Declined, "Declined")]
    [InlineData(null, "Inactive")]
    public async Task Declined_or_unexplained_missing_requests_cannot_be_resent(ConnectionInvitationStatus? providerStatus, string expectedState)
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(new LinkedInContactStatus(false, false, false, SentInvitationStatus: providerStatus), null, []);
        var synced = await JsonAsync(await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null));
        Assert.Equal(expectedState, synced["connectionState"]!.ToString());
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(new LinkedInContactStatus(false, false, false), null, []);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/linkedin/retry", new { confirmNotSent = true })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/linkedin/connection-request", new { generateNote = true })).StatusCode);
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
    }

    [Fact]
    public async Task Synchronization_inserts_missing_ids_once_without_overwriting_messages_or_campaign_stage()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        var now = DateTimeOffset.UtcNow.AddSeconds(1);
        var outgoing = new LinkedInSyncMessage("manual-out", "I sent this in LinkedIn", now, true);
        var incoming = new LinkedInSyncMessage("manual-in", "Yes, interested", now.AddSeconds(1), false);
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(
            new LinkedInContactStatus(true, true, false),
            new LinkedInChat("test-account", "manual-chat", "provider-chat", null),
            [outgoing, incoming, incoming]);
        (await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null)).EnsureSuccessStatusCode();
        app.LinkedIn.SyncSnapshot = app.LinkedIn.SyncSnapshot with
        {
            Messages = [outgoing with { IsDeleted = true }, incoming with { Text = "Changed text", IsEdited = true }]
        };
        (await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null)).EnsureSuccessStatusCode();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Messages.Where(x => x.EnrollmentId == enrollment).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, x => x.Kind == MessageKind.Manual && x.Content == outgoing.Text && !x.IsDeleted);
        Assert.Contains(rows, x => x.Kind == MessageKind.Reply && x.Content == incoming.Text && !x.IsEdited && x.Analyzed);
        var candidate = (await db.Enrollments.FindAsync(enrollment))!;
        Assert.Equal(CandidateStatus.Screened, candidate.Status);
        Assert.False(candidate.HasSentInitialMessage);
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Empty(app.LinkedIn.Invitations);
    }

    [Fact]
    public async Task Synchronization_imports_a_new_reply_for_the_reply_worker()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).EnsureSuccessStatusCode();
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(
            new LinkedInContactStatus(true, true, false),
            new LinkedInChat("test-account", "reply-chat", "provider-chat", null),
            [new LinkedInSyncMessage("new-reply", "Yes, interested", DateTimeOffset.UtcNow.AddSeconds(1), false)]);
        (await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null)).EnsureSuccessStatusCode();
        Assert.Equal("AwaitingReply", await GetStatusAsync(client, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Equal("Interested", await GetStatusAsync(client, enrollment));
    }

    [Fact]
    public async Task Withdrawn_invitation_is_inactive_until_an_explicit_candidate_retry()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().LinkedInConversations.Where(x => x.EnrollmentId == enrollment)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConnectionRequestedAt, DateTimeOffset.UtcNow.AddMinutes(-10))
                    .SetProperty(x => x.ConnectionRequestDelivery, DeliveryStatus.Unknown));
        }

        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(new LinkedInContactStatus(false, false, false, SentInvitationStatus: ConnectionInvitationStatus.Withdrawn), null, []);
        var synced = await JsonAsync(await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null));
        Assert.Equal("Withdrawn", synced["connectionState"]!.ToString());
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
        await app.ClearSendDelayAsync(campaign);
        (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/linkedin/retry", new { confirmNotSent = false })).EnsureSuccessStatusCode();
        Assert.Equal(2, app.LinkedIn.Invitations.Count);
    }

    [Fact]
    public async Task Synchronization_does_not_match_unknown_delivery_by_text_or_automatically_retry_it()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        app.LinkedIn.FailAfterSend = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).StatusCode);
        var sent = Assert.Single(app.LinkedIn.Sent);
        app.LinkedIn.FailAfterSend = false;
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(
            new LinkedInContactStatus(true, true, false),
            new LinkedInChat("test-account", "reconciled-chat", "provider-chat", null),
            [new LinkedInSyncMessage("found-message", sent.Text, DateTimeOffset.UtcNow.AddSeconds(1), true)]);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/linkedin/retry", new { confirmNotSent = false })).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
        Assert.Equal("Screened", await GetStatusAsync(client, enrollment));
        using var scope = app.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Messages.Where(x => x.EnrollmentId == enrollment).ToListAsync();
        Assert.Contains(rows, x => x.Kind == MessageKind.Initial && x.Delivery == DeliveryStatus.Unknown && x.ExternalId == null);
        Assert.Contains(rows, x => x.Kind == MessageKind.Manual && x.ExternalId == "found-message");
    }

    [Fact]
    public async Task Unknown_delivery_requires_explicit_confirmation_and_failed_sync_never_retries()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        app.LinkedIn.FailAfterSend = true;
        await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null);
        app.LinkedIn.FailAfterSend = false;
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Messages.Where(x => x.EnrollmentId == enrollment)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AttemptedAt, DateTimeOffset.UtcNow.AddMinutes(-10)));
        }

        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(new LinkedInContactStatus(true, true, false), null, []);
        var path = $"/api/enrollments/{enrollment}/linkedin/retry";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { confirmNotSent = false })).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
        app.LinkedIn.FailSynchronization = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(path, new { confirmNotSent = true })).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LinkedInConversations.SingleAsync(x => x.EnrollmentId == enrollment);
            Assert.Null(row.SynchronizationLeaseUntil);
            Assert.NotNull(row.SynchronizationError);
        }

        app.LinkedIn.FailSynchronization = false;
        await app.ClearSendDelayAsync(campaign);
        (await client.PostAsJsonAsync(path, new { confirmNotSent = true })).EnsureSuccessStatusCode();
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Historical_messages_do_not_start_a_new_campaign_and_sync_schedules_ten_minutes_later()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client, false);
        await ScreenProfileAsync(client, enrollment);
        app.LinkedIn.SyncSnapshot = new LinkedInSyncSnapshot(
            new LinkedInContactStatus(true, true, false),
            new LinkedInChat("test-account", "old-chat", "provider-old", null),
            [
                new LinkedInSyncMessage("old-out", "Old discussion", DateTimeOffset.UtcNow.AddYears(-1), true),
                new LinkedInSyncMessage("old-in", "An old reply", DateTimeOffset.UtcNow.AddYears(-1).AddDays(1), false)
            ]);
        var before = DateTimeOffset.UtcNow;
        (await client.PostAsync($"/api/enrollments/{enrollment}/linkedin/sync", null)).EnsureSuccessStatusCode();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.Enrollments.FindAsync(enrollment))!.HasSentInitialMessage);
        Assert.All(await db.Messages.Where(x => x.EnrollmentId == enrollment && x.Kind == MessageKind.Reply).ToListAsync(), x => Assert.True(x.Analyzed));
        var conversation = await db.LinkedInConversations.SingleAsync(x => x.EnrollmentId == enrollment);
        Assert.InRange(conversation.NextSynchronizationAt!.Value, before.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.DoesNotContain(enrollment, await scope.ServiceProvider.GetRequiredService<ILinkedInConversationRepository>().GetSynchronizationIdsAsync("test-account", 10000, CancellationToken.None));
    }
}