using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;
using Xunit;
using ProfileSearchResult = TalentScout.Api.Tests.TestProfileSearchPage;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    private static async Task AdvanceAsync(TestApi app, Guid enrollment)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PipelineCoordinator>().AdvanceAsync(enrollment, CancellationToken.None);
    }

    private static async Task<bool> IsScheduledAsync(TestApi app, Guid enrollment)
    {
        using var scope = app.Services.CreateScope();
        var ids = await scope.ServiceProvider.GetRequiredService<IEnrollmentRepository>().GetPendingIdsAsync(10000, CancellationToken.None);

        return ids.Contains(enrollment);
    }

    private async Task<(Guid Campaign, Guid Enrollment)> InterestedAsync(HttpClient client, bool autoInvitations = false)
    {
        var pair = await SetupAsync(client, true, autoInvitations);
        await ScreenProfileAsync(client, pair.Enrollment);
        await SendIntroductionAsync(client, pair.Enrollment);
        await ReplyAsync(client, pair.Enrollment);
        await AnalyzeReplyAsync(client, pair.Enrollment);

        return pair;
    }

    [Fact]
    public async Task Manual_first_contact_waits_until_selected_and_auto_mode_picks_up_old_screened_candidates()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client, false, false);
        await ScreenProfileAsync(client, enrollment);
        Assert.False(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).StatusCode);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveCandidates = true })).EnsureSuccessStatusCode();
        Assert.True(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Sent);
        var status = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}"));
        Assert.Equal("Automatic", status["approvalStatus"]!.ToString());
    }

    [Fact]
    public async Task Manual_outreach_approval_and_exclusion_override_the_first_checkbox()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client, false, false);
        await ScreenProfileAsync(client, enrollment);
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/outreach/start", new { enrollmentIds = new[] { enrollment } })).EnsureSuccessStatusCode();
        Assert.True(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Single(app.LinkedIn.Sent);
        var other = await SetupAsync(client, true, false);
        await ScreenProfileAsync(client, other.Enrollment);
        (await client.PostAsJsonAsync($"/api/campaigns/{other.Campaign}/outreach/exclude", new { enrollmentIds = new[] { other.Enrollment } })).EnsureSuccessStatusCode();
        Assert.False(await IsScheduledAsync(app, other.Enrollment));
        await AdvanceAsync(app, other.Enrollment);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{other.Enrollment}/send-introduction", null)).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Invitation_requires_its_own_approval_and_first_checkbox_does_not_block_it()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await InterestedAsync(client);
        Assert.False(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)).StatusCode);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveCandidates = false })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/interview-invitations/approve", new { enrollmentIds = new[] { enrollment } })).EnsureSuccessStatusCode();
        var queue = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates?status=Interested&invitationApprovalStatus=Approved"));
        Assert.Single(queue.AsArray());
        Assert.True(await IsScheduledAsync(app, enrollment));
        await app.ClearSendDelayAsync(campaign);
        await AdvanceAsync(app, enrollment);
        Assert.Equal("InterviewInvitationSent", await GetStatusAsync(client, enrollment));
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Invitation_toggle_picks_up_waiting_candidates_and_disabling_blocks_prepared_messages()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await InterestedAsync(client);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveInterviewInvitations = true })).EnsureSuccessStatusCode();
        Assert.True(await IsScheduledAsync(app, enrollment));
        // Campaign send interval makes the command prepare a durable Pending invitation.
        Assert.Equal("Interested", await SendInvitationAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveInterviewInvitations = false })).EnsureSuccessStatusCode();
        await app.ClearSendDelayAsync(campaign);
        Assert.False(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveInterviewInvitations = true })).EnsureSuccessStatusCode();
        await AdvanceAsync(app, enrollment);
        Assert.Equal(2, app.LinkedIn.Sent.Count);
        var status = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}"));
        Assert.Equal("Automatic", status["invitationApprovalStatus"]!.ToString());
    }

    [Fact]
    public async Task Rejected_invitation_is_held_even_with_auto_mode_until_explicitly_approved()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await InterestedAsync(client, true);
        var ids = new { enrollmentIds = new[] { enrollment } };
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/interview-invitations/reject", ids)).EnsureSuccessStatusCode();
        await app.ClearSendDelayAsync(campaign);
        Assert.False(await IsScheduledAsync(app, enrollment));
        await AdvanceAsync(app, enrollment);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/interview-invitations/approve", ids)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveInterviewInvitations = false })).EnsureSuccessStatusCode();
        await AdvanceAsync(app, enrollment);
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Approval_selection_is_atomic_and_rejects_foreign_candidates_and_pending_replies()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var first = await InterestedAsync(client);
        var other = await InterestedAsync(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/campaigns/{first.Campaign}/interview-invitations/approve", new { enrollmentIds = new[] { first.Enrollment, other.Enrollment } })).StatusCode);
        var status = await JsonAsync(await client.GetAsync($"/api/enrollments/{first.Enrollment}"));
        Assert.Equal("Pending", status["invitationApprovalStatus"]!.ToString());
        await ReplyAsync(client, first.Enrollment);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/campaigns/{first.Campaign}/interview-invitations/approve", new { enrollmentIds = new[] { first.Enrollment } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/campaigns/{first.Campaign}/interview-invitations/approve", new { enrollmentIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/campaigns/{first.Campaign}/interview-invitations/reject", new { enrollmentIds = new[] { first.Enrollment, first.Enrollment } })).StatusCode);
    }

    [Fact]
    public async Task Partial_settings_preserve_the_other_flag_and_empty_settings_are_invalid()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client);
        var settings = await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveCandidates = false }));
        Assert.False(settings["autoApproveCandidates"]!.GetValue<bool>());
        Assert.True(settings["autoApproveInterviewInvitations"]!.GetValue<bool>());
        settings = await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveInterviewInvitations = false }));
        Assert.False(settings["autoApproveCandidates"]!.GetValue<bool>());
        Assert.False(settings["autoApproveInterviewInvitations"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { })).StatusCode);
    }

    private static CandidateProfile Profile(string? id = null)
    {
        id ??= Guid.NewGuid().ToString();

        return new CandidateProfile(id, "https://www.linkedin.com/in/" + id, "Search Candidate", "Developer", "Europe", "C#");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delivery_claim_reloads_permissions_when_the_handler_has_stale_campaign_settings(bool invitation)
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollmentId) = invitation ? await InterestedAsync(client, true) : await SetupAsync(client);
        if (invitation)
            await SendInvitationAsync(client, enrollmentId);
        else
        {
            await ScreenProfileAsync(client, enrollmentId);
            // Hold the first message behind the campaign interval so it remains Pending.
            using var delayScope = app.Services.CreateScope();
            var campaigns = delayScope.ServiceProvider.GetRequiredService<ICampaignRepository>();
            var item = (await campaigns.FindAsync(campaign, CancellationToken.None))!;
            item.NextOutboundAt = DateTimeOffset.UtcNow.AddMinutes(10);
            await delayScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
            await SendIntroductionAsync(client, enrollmentId);
        }

        using var staleScope = app.Services.CreateScope();
        var enrollment = (await staleScope.ServiceProvider.GetRequiredService<IEnrollmentRepository>().GetDetailsAsync(enrollmentId, CancellationToken.None))!;
        var messages = staleScope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var message = (await messages.FindOutgoingAsync(enrollmentId, invitation ? MessageKind.Invitation : MessageKind.Initial, CancellationToken.None))!;
        Assert.Equal(DeliveryStatus.Pending, message.Delivery);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/approval-settings", new { autoApproveCandidates = false, autoApproveInterviewInvitations = false })).EnsureSuccessStatusCode();
        await app.ClearSendDelayAsync(campaign);
        Assert.False(await messages.TryClaimDeliveryAsync(enrollment, message, DateTimeOffset.UtcNow, 1, 50, CancellationToken.None));
        await messages.ReloadAsync(message, CancellationToken.None);
        Assert.Equal(DeliveryStatus.Pending, message.Delivery);
        Assert.Equal(invitation ? 1 : 0, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Concurrent_invitation_commands_deliver_the_link_only_once()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await InterestedAsync(client, true);
        await app.ClearSendDelayAsync(campaign);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)));
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict, r.StatusCode.ToString()));
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Search_refills_past_existing_enrollments_without_changing_their_status()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client, false, false);
        var old = Profile();
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([old], null));
        var first = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/search", new { limit = 1 }));
        var oldId = Guid.Parse(first["enrollmentIds"]![0]!.ToString());
        await ScreenProfileAsync(client, oldId);
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/outreach/exclude", new { enrollmentIds = new[] { oldId } })).EnsureSuccessStatusCode();
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([old, Profile()], "page-two"));
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([Profile()], null));
        var result = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/search", new { limit = 2 }));
        Assert.Equal(2, result["addedCount"]!.GetValue<int>());
        Assert.Equal(1, result["skippedCount"]!.GetValue<int>());
        Assert.Equal(2, result["pagesScanned"]!.GetValue<int>());
        Assert.Equal("TargetReached", result["stopReason"]!.ToString());
        Assert.Equal("page-two", app.LinkedIn.Searches[^1].Cursor);
        var existing = await JsonAsync(await client.GetAsync($"/api/enrollments/{oldId}"));
        Assert.Equal("Excluded", existing["approvalStatus"]!.ToString());
        Assert.Equal(first["searchBatchId"]!.ToString(), existing["searchBatchId"]!.ToString());
        var batch = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates?searchBatchId={result["searchBatchId"]}"));
        Assert.Equal(2, batch.AsArray().Count);
    }

    [Theory]
    [InlineData(true, "RepeatedCursor")]
    [InlineData(false, "ScanLimitReached")]
    public async Task Search_stops_on_repeated_cursors_or_scan_limit(bool repeated, string expected)
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client, false, false);
        var old = Profile();
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([old], null));
        await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/search", new { limit = 1 }));
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([old], "same"));
        app.LinkedIn.SearchPages.Enqueue(new ProfileSearchResult([old], "same"));
        var result = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/search", new { limit = 1, maxProfilesToScan = repeated ? 10 : 1 }));
        Assert.Equal(expected, result["stopReason"]!.ToString());
        Assert.Equal(0, result["addedCount"]!.GetValue<int>());
    }
}