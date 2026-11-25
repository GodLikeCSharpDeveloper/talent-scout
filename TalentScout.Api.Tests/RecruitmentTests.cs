using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ardalis.Result;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private async Task<(Guid Campaign, Guid Enrollment)> SetupAsync(HttpClient client, bool autoApproveCandidates = true, bool autoApproveInterviewInvitations = true)
    {
        var campaign = await JsonAsync(
            await client.PostAsJsonAsync(
                "/api/campaigns",
                new
                {
                    autoApproveCandidates, autoApproveInterviewInvitations,
                    name = "Backend team", jobTitle = ".NET developer", description = "Build APIs", requirements = "C# and PostgreSQL",
                    searchQuery = ".NET", location = "Europe", recruiter = "Recruiter", interviewUrl = "https://calendly.com/example/interview"
                }));
        var campaignId = Guid.Parse(campaign["id"]!.ToString());
        var candidate = await JsonAsync(
            await client.PostAsJsonAsync(
                "/api/candidates",
                new
                {
                    externalId = Guid.NewGuid().ToString(), profileUrl = $"https://www.linkedin.com/in/{Guid.NewGuid()}/?trk=test",
                    name = "Test Candidate", headline = ".NET developer", location = "Europe", experience = "Five years of C#"
                }));
        var enrollment = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/candidates", new { candidateId = candidate["id"]!.ToString() }));
        (await client.PutAsJsonAsync($"/api/campaigns/{campaignId}/status", new { status = "Active" })).EnsureSuccessStatusCode();

        return (campaignId, Guid.Parse(enrollment["id"]!.ToString()));
    }

    private static async Task<JsonNode> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");

        return JsonNode.Parse(text)!;
    }

    private static async Task<string> ExecuteCommandAsync(HttpClient client, Guid enrollment, string command)
    {
        return (await JsonAsync(await client.PostAsync($"/api/enrollments/{enrollment}/{command}", null)))["status"]!.ToString();
    }

    private static Task<string> ScreenProfileAsync(HttpClient client, Guid enrollment)
    {
        return ExecuteCommandAsync(client, enrollment, "screen-profile");
    }

    private static Task<string> SendIntroductionAsync(HttpClient client, Guid enrollment)
    {
        return ExecuteCommandAsync(client, enrollment, "send-introduction");
    }

    private static Task<string> AnalyzeReplyAsync(HttpClient client, Guid enrollment)
    {
        return ExecuteCommandAsync(client, enrollment, "analyze-reply");
    }

    private static Task<string> SendInvitationAsync(HttpClient client, Guid enrollment)
    {
        return ExecuteCommandAsync(client, enrollment, "send-interview-invitation");
    }

    private static async Task<string> GetStatusAsync(HttpClient client, Guid enrollment)
    {
        return (await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}")))["status"]!.ToString();
    }

    private static async Task ReplyAsync(HttpClient client, Guid enrollment, string? externalId = null)
    {
        (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/messages/replies", new { externalMessageId = externalId ?? Guid.NewGuid().ToString(), content = "Tell me more" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Interested_candidate_receives_one_invitation_and_booking_is_idempotent()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        Assert.Equal("Screened", await ScreenProfileAsync(client, enrollment));
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Equal("AwaitingReply", await SendIntroductionAsync(client, enrollment));
        await ReplyAsync(client, enrollment, "positive-" + enrollment);
        await ReplyAsync(client, enrollment, "positive-" + enrollment);
        Assert.Equal("Interested", await AnalyzeReplyAsync(client, enrollment));
        await app.ClearSendDelayAsync(campaign);
        Assert.Equal("InterviewInvitationSent", await SendInvitationAsync(client, enrollment));
        await SendInvitationAsync(client, enrollment);
        Assert.Equal(2, app.LinkedIn.Sent.Count);
        var booking = new { enrollmentId = enrollment, provider = "calendly", externalBookingId = Guid.NewGuid().ToString(), startsAt = DateTimeOffset.UtcNow.AddDays(1), endsAt = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30) };
        var first = await JsonAsync(await client.PostAsJsonAsync("/api/interviews/bookings", booking));
        var second = await JsonAsync(await client.PostAsJsonAsync("/api/interviews/bookings", booking));
        Assert.Equal(first["id"]!.ToString(), second["id"]!.ToString());
        Assert.Equal("InterviewBooked", await GetStatusAsync(client, enrollment));
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Theory]
    [InlineData(Interest.NotInterested, 0.99, "NotInterested")]
    [InlineData(Interest.Ambiguous, 0.99, "NeedsReview")]
    [InlineData(Interest.Interested, 0.2, "NeedsReview")]
    public async Task Negative_ambiguous_and_low_confidence_replies_do_not_trigger_invitations(Interest interest, double confidence, string expected)
    {
        await using var app = new TestApi(database.ConnectionString);
        app.Ai.Interest = interest;
        app.Ai.Confidence = (decimal)confidence;
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        Assert.Equal(expected, await AnalyzeReplyAsync(client, enrollment));
        await app.ClearSendDelayAsync(campaign);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)).StatusCode);
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Unknown_send_outcome_is_not_automatically_retried()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.LinkedIn.FailAfterSend = true;
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).StatusCode);
        await app.ClearSendDelayAsync(campaign);
        await SendIntroductionAsync(client, enrollment);
        Assert.Single(app.LinkedIn.Sent);
        var messages = await JsonAsync(await client.GetAsync($"/api/enrollments/{enrollment}/messages"));
        Assert.Equal("Unknown", messages[0]!["delivery"]!.ToString());
    }

    [Fact]
    public async Task Concurrent_processing_cannot_send_two_initial_messages()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)));
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict, r.StatusCode.ToString()));
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Paused_campaign_cannot_send_messages()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Paused" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).StatusCode);
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Rejected_profile_cannot_be_contacted()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.Ai.MeetsRequirements = false;
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        Assert.Equal("Rejected", await ScreenProfileAsync(client, enrollment));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null)).StatusCode);
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Api_requires_key_and_generates_openapi()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var spec = await JsonAsync(await client.GetAsync("/openapi/v1.json"));
        Assert.NotNull(spec["paths"]!["/api/campaigns"]);
        Assert.Null(spec["paths"]!["/api/enrollments/{id}/process"]);
        Assert.NotNull(spec["paths"]!["/api/enrollments/{id}/screen-profile"]);
        Assert.NotNull(spec["paths"]!["/api/enrollments/{id}/send-introduction"]);
        Assert.NotNull(spec["paths"]!["/api/enrollments/{id}/analyze-reply"]);
        Assert.NotNull(spec["paths"]!["/api/enrollments/{id}/send-interview-invitation"]);
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Invalid_booking_and_page_are_rejected()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(
                "/api/interviews/bookings",
                new
                {
                    enrollmentId = enrollment, provider = "calendly", externalBookingId = Guid.NewGuid().ToString(), startsAt = DateTimeOffset.UtcNow, endsAt = DateTimeOffset.UtcNow.AddHours(1)
                })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/candidates?pageSize=10000")).StatusCode);
    }

    [Fact]
    public async Task Manual_review_can_confirm_interest_without_repeating_the_initial_message()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.Ai.Interest = Interest.Ambiguous;
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        Assert.Equal("NeedsReview", await AnalyzeReplyAsync(client, enrollment));
        (await client.PostAsJsonAsync($"/api/enrollments/{enrollment}/review", new { decision = "Interested", reason = "Candidate confirmed interest during a call." })).EnsureSuccessStatusCode();
        await app.ClearSendDelayAsync(campaign);
        Assert.Equal("InterviewInvitationSent", await SendInvitationAsync(client, enrollment));
        Assert.Equal(2, app.LinkedIn.Sent.Count);
    }

    [Fact]
    public async Task Send_interval_defers_an_invitation_until_the_campaign_is_allowed_to_send()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        await AnalyzeReplyAsync(client, enrollment);
        Assert.Equal("Interested", await SendInvitationAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Stopped_campaign_is_terminal()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Stopped" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Active" })).StatusCode);
    }

    [Fact]
    public async Task Duplicate_profile_and_enrollment_return_the_same_identity()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client);
        var slug = Guid.NewGuid().ToString();
        var profile = new { externalId = slug, profileUrl = "https://linkedin.com/in/" + slug + "/?trk=one", name = "Candidate", headline = "Developer", location = "Europe", experience = "C#" };
        var first = await JsonAsync(await client.PostAsJsonAsync("/api/candidates", profile));
        var second = await JsonAsync(await client.PostAsJsonAsync("/api/candidates", profile with { profileUrl = "https://www.linkedin.com/in/" + slug + "?trk=two" }));
        Assert.Equal(first["id"]!.ToString(), second["id"]!.ToString());
        var request = new { candidateId = first["id"]!.ToString() };
        var firstEnrollment = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/candidates", request));
        var secondEnrollment = await JsonAsync(await client.PostAsJsonAsync($"/api/campaigns/{campaign}/candidates", request));
        Assert.Equal(firstEnrollment["id"]!.ToString(), secondEnrollment["id"]!.ToString());
    }

    [Fact]
    public async Task Expected_failures_are_mapped_to_problem_details_with_a_trace_identifier()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var response = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("NotFound", problem!["title"]!.ToString());
        Assert.Equal(404, problem["status"]!.GetValue<int>());
        Assert.NotNull(problem["traceId"]);
    }

    [Fact]
    public async Task Controller_query_binding_preserves_pagination_defaults_and_status_filter()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        var matching = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates?status=Screened"));
        Assert.Single(matching.AsArray());
        var empty = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates?status=Interested"));
        Assert.Empty(empty.AsArray());
    }

    [Fact]
    public async Task Fluent_validation_errors_are_returned_as_field_errors()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var response = await client.PostAsJsonAsync(
            "/api/campaigns",
            new
            {
                name = "Campaign", jobTitle = "Developer", description = "Build APIs", requirements = "C#",
                searchQuery = "C#", location = "Europe", recruiter = "Recruiter", minimumFitScore = 1.5m
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(problem!["errors"]!["MinimumFitScore"]);
    }

    [Fact]
    public async Task Invalid_profile_is_rejected_with_field_errors_before_it_is_saved()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var response = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                externalId = Guid.NewGuid().ToString(), profileUrl = "https://example.com/in/test", name = " ",
                headline = "Developer", location = "Europe", experience = "C#"
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(problem!["errors"]!["ProfileUrl"]);
        Assert.NotNull(problem["errors"]!["Name"]);
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Empty_reply_returns_field_errors_before_enrollment_lookup()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/enrollments/{Guid.NewGuid()}/messages/replies",
            new
            {
                externalMessageId = "", content = " "
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(problem!["errors"]!["ExternalMessageId"]);
        Assert.NotNull(problem["errors"]!["Content"]);
    }

    [Fact]
    public async Task Invalid_ai_confidence_cannot_trigger_an_invitation()
    {
        await using var app = new TestApi(database.ConnectionString);
        app.Ai.Confidence = 2;
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        var response = await client.PostAsync($"/api/enrollments/{enrollment}/analyze-reply", null);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Introduction_command_cannot_implicitly_screen_a_profile()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        var response = await client.PostAsync($"/api/enrollments/{enrollment}/send-introduction", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Found", await GetStatusAsync(client, enrollment));
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Repeated_screening_never_sends_a_message()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        Assert.Equal("Screened", await ScreenProfileAsync(client, enrollment));
        Assert.Equal("Screened", await ScreenProfileAsync(client, enrollment));
        Assert.Empty(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Introduction_command_never_analyzes_a_reply_or_sends_an_invitation()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        Assert.Equal("AwaitingReply", await SendIntroductionAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/send-interview-invitation", null)).StatusCode);
        Assert.Equal("Interested", await AnalyzeReplyAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Background_coordinator_calls_the_same_explicit_stage_handlers()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (_, enrollment) = await SetupAsync(client);
        using (var scope = app.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<PipelineCoordinator>()
                .AdvanceAsync(enrollment, CancellationToken.None);
            Assert.Equal(ResultStatus.Ok, result!.Status);
        }

        Assert.Equal("Screened", await GetStatusAsync(client, enrollment));
        Assert.Empty(app.LinkedIn.Sent);
        using (var scope = app.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<PipelineCoordinator>()
                .AdvanceAsync(enrollment, CancellationToken.None);
            Assert.Equal(ResultStatus.Ok, result!.Status);
        }

        Assert.Equal("AwaitingReply", await GetStatusAsync(client, enrollment));
        Assert.Single(app.LinkedIn.Sent);
    }

    [Fact]
    public async Task Daily_limit_prevents_further_delivery_attempts()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        await ScreenProfileAsync(client, enrollment);
        await SendIntroductionAsync(client, enrollment);
        await ReplyAsync(client, enrollment);
        await AnalyzeReplyAsync(client, enrollment);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 49; i++)
            {
                var candidateId = Guid.NewGuid();
                db.Candidates.Add(new Candidate { Id = candidateId, ExternalId = candidateId.ToString(), ProfileUrl = "https://www.linkedin.com/in/" + candidateId });
                var item = new Enrollment { CampaignId = campaign, CandidateId = candidateId };
                db.Enrollments.Add(item);
                db.Messages.Add(new Message { EnrollmentId = item.Id, Kind = MessageKind.Initial, Delivery = DeliveryStatus.Sent, AttemptedAt = DateTimeOffset.UtcNow });
            }

            await db.SaveChangesAsync();
        }

        await app.ClearSendDelayAsync(campaign);
        await SendInvitationAsync(client, enrollment);
        Assert.Single(app.LinkedIn.Sent);
    }
}