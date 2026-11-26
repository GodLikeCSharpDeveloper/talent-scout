using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Shared;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using TalentScout.Data.Repositories;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Fact]
    public async Task Incoming_reply_is_ready_for_analysis_despite_outreach_delay_and_blocks_new_messages()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        await ReplyAsync(client, enrollment);
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
        await repository.ScheduleNextAttemptAsync(enrollment, DateTimeOffset.UtcNow.AddHours(6), CancellationToken.None);
        Assert.Contains(enrollment, await repository.GetPendingReplyIdsAsync(10000, CancellationToken.None));
        Assert.DoesNotContain(enrollment, await repository.GetPendingMessageIdsAsync(10000, CancellationToken.None));
    }

    [Fact]
    public async Task Reply_to_connection_note_is_analyzed_without_sending_a_duplicate_introduction()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conversation = await db.LinkedInConversations.SingleAsync(x => x.EnrollmentId == enrollment);
            app.LinkedIn.Chat = new LinkedInChat("test-account", "early-reply-chat", "provider-chat", conversation.RecipientId);
            var item = new LinkedInWebhookEvent
            {
                AccountId = "test-account", ChatId = "early-reply-chat", SenderId = conversation.RecipientId,
                ExternalMessageId = Guid.NewGuid().ToString(), Content = "Yes, tell me more about this role.", OccurredAt = DateTimeOffset.UtcNow
            };
            db.LinkedInWebhookEvents.Add(item);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ProcessUnipileWebhookHandler>().HandleAsync(item.Id, CancellationToken.None);
            Assert.NotNull(item.ProcessedAt);
        }

        await AdvanceAsync(app, enrollment);
        Assert.Equal("Interested", await GetStatusAsync(client, enrollment));
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Single(app.LinkedIn.Invitations);
    }

    [Fact]
    public async Task Three_workers_are_registered_with_disjoint_screening_sending_and_reply_queries()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        Assert.Contains(app.Services.GetServices<IHostedService>(), x => x is ProfileScreeningWorker);
        Assert.Contains(app.Services.GetServices<IHostedService>(), x => x is MessageSendingWorker);
        Assert.Contains(app.Services.GetServices<IHostedService>(), x => x is ReplyAnalysisWorker);
        using (var scope = app.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
            Assert.Contains(enrollment, await repository.GetPendingProfileIdsAsync(10000, CancellationToken.None));
            Assert.DoesNotContain(enrollment, await repository.GetPendingMessageIdsAsync(10000, CancellationToken.None));
        }

        await ScreenProfileAsync(client, enrollment);
        using (var scope = app.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>();
            Assert.DoesNotContain(enrollment, await repository.GetPendingProfileIdsAsync(10000, CancellationToken.None));
            Assert.Contains(enrollment, await repository.GetPendingMessageIdsAsync(10000, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Approval_sends_one_AI_connection_note_then_introduction_after_acceptance()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client, false);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        Assert.Empty(app.LinkedIn.Invitations);
        Assert.Equal(0, app.Ai.ConnectionNoteCalls);

        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/outreach/start", new { enrollmentIds = new[] { enrollment } })).EnsureSuccessStatusCode();
        await AdvanceAsync(app, enrollment);
        Assert.Equal(app.Ai.ConnectionNote, Assert.Single(app.LinkedIn.Invitations).Note);
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Equal("Screened", await GetStatusAsync(client, enrollment));
        var conversation = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}/linkedin/conversation"));
        Assert.Equal(app.Ai.ConnectionNote, conversation["connectionNote"]!.ToString());
        Assert.Equal("Sent", conversation["connectionRequestDelivery"]!.ToString());
        var contact = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}/linkedin/contact-status"));
        Assert.True(contact["invitationPending"]!.GetValue<bool>());
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
        Assert.Equal(1, app.Ai.ConnectionNoteCalls);

        app.LinkedIn.ContactStatus = new LinkedInContactStatus(true, true, false);
        await app.ClearSendDelayAsync(campaign);
        await AdvanceAsync(app, enrollment);
        Assert.Equal("AwaitingReply", await GetStatusAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
        Assert.NotEqual(app.Ai.ConnectionNote, app.LinkedIn.Sent[0].Text);
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Unknown_invitation_outcome_is_visible_and_never_automatically_retried()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        app.LinkedIn.FailAfterInvitation = true;
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        var error = await Assert.ThrowsAsync<ApiException>(() => AdvanceAsync(app, enrollment));
        Assert.Equal(503, error.Status);
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
        Assert.Empty(app.LinkedIn.Sent);
        var contact = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}/linkedin/contact-status"));
        Assert.Equal("Unknown", contact["connectionRequestDelivery"]!.ToString());
    }

    [Fact]
    public async Task Existing_pending_invitation_or_paused_campaign_sends_nothing()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, true);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await AdvanceAsync(app, enrollment);
        Assert.Empty(app.LinkedIn.Invitations);
        Assert.Equal(0, app.Ai.ConnectionNoteCalls);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Paused" })).EnsureSuccessStatusCode();
        await AdvanceAsync(app, enrollment);
        Assert.Empty(app.LinkedIn.Invitations);
        Assert.Equal(0, app.Ai.ConnectionNoteCalls);
    }

    [Fact]
    public async Task Long_generated_note_is_rejected_before_any_invitation()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        app.Ai.ConnectionNote = new string('x', 201);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        var error = await Assert.ThrowsAsync<ApiException>(() => AdvanceAsync(app, enrollment));
        Assert.Equal(502, error.Status);
        Assert.Empty(app.LinkedIn.Invitations);
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Delayed_invitation_reuses_saved_note_and_respects_outbound_spacing()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.ContactStatus = new LinkedInContactStatus(false, false, false);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Campaigns.Where(x => x.Id == campaign)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextOutboundAt, DateTimeOffset.UtcNow.AddHours(1)));
        }

        await AdvanceAsync(app, enrollment);
        Assert.Empty(app.LinkedIn.Invitations);
        Assert.Equal(1, app.Ai.ConnectionNoteCalls);
        await app.ClearSendDelayAsync(campaign);
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Invitations);
        Assert.Equal(1, app.Ai.ConnectionNoteCalls);
    }
}