using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Fact]
    public async Task Ui_prompt_is_persisted_and_used_by_every_ai_stage_without_restart()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client);
        const string prompt = "Пиши коротко українською.";
        var changed = await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = " " + prompt + " " }));
        Assert.Equal(prompt, changed["systemPrompt"]!.ToString());
        await ScreenProfileAsync(client, enrollment);
        Assert.Equal(prompt, app.Ai.LastVacancy!.SystemPrompt);
        await SendIntroductionAsync(client, enrollment);
        Assert.Equal(prompt, app.Ai.LastVacancy!.SystemPrompt);
        const string updated = "Аналізуй тільки явний інтерес.";
        await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = updated }));
        await ReplyAsync(client, enrollment);
        await AnalyzeReplyAsync(client, enrollment);
        Assert.Equal(updated, app.Ai.LastVacancy!.SystemPrompt);
        var saved = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}"));
        Assert.Equal(updated, saved["systemPrompt"]!.ToString());
        await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = "" }));
        saved = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}"));
        Assert.Equal("", saved["systemPrompt"]!.ToString());
    }

    [Fact]
    public async Task Prompt_validation_and_campaign_state_are_respected()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = new string('x', 20001) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/campaigns/{Guid.NewGuid()}/ai-settings", new { systemPrompt = "Test" })).StatusCode);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Stopped" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/ai-settings", new { systemPrompt = "Test" })).StatusCode);
    }
}