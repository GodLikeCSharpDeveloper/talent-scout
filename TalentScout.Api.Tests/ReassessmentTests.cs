using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Fact]
    public async Task Reassessment_replaces_a_rejected_result_using_current_campaign_settings_while_paused()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        app.Ai.MeetsRequirements = false;
        Assert.Equal("Rejected", await ScreenProfileAsync(client, enrollment));
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Paused" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}", new { name = "Updated", jobTitle = "Developer", description = "Build APIs", requirements = "Updated requirements", searchQuery = "C#", location = "Europe", recruiter = "Recruiter", minimumFitScore = 0.4m, systemPrompt = "Updated instructions" })).EnsureSuccessStatusCode();
        app.Ai.MeetsRequirements = true;
        var result = await JsonAsync(await client.PostAsync($"/api/enrollments/{enrollment}/rescreen-profile", null));
        Assert.Equal("Screened", result["status"]!.ToString());
        Assert.Equal(2, app.Ai.AssessmentCalls);
        Assert.Equal("Updated requirements", app.Ai.LastVacancy!.Requirements);
        Assert.Equal("Updated instructions", app.Ai.LastVacancy.SystemPrompt);
        Assert.Empty(app.LinkedIn.Sent);
        // Normal screen-profile remains idempotent; only the new explicit command reruns AI.
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Active" })).EnsureSuccessStatusCode();
        Assert.Equal("Screened", await ScreenProfileAsync(client, enrollment));
        Assert.Equal(2, app.Ai.AssessmentCalls);
    }

    [Fact]
    public async Task Reassessment_does_not_reset_started_outreach_or_stopped_campaigns()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client, false);
        await ScreenProfileAsync(client, enrollment);
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/outreach/start", new { enrollmentIds = new[] { enrollment } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/rescreen-profile", null)).StatusCode);
        Assert.Equal(1, app.Ai.AssessmentCalls);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Stopped" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/enrollments/{enrollment}/rescreen-profile", null)).StatusCode);
        Assert.Equal("Screened", await GetStatusAsync(client, enrollment));
    }
}