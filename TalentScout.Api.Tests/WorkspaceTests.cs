using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Search_completion_pushes_a_notification_with_count_and_approval_mode(bool automatic)
    {
        using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client, automatic);
        var ticket = await JsonAsync(await client.PostAsync("/api/realtime/ticket", null));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var socket = await app.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/hubs/workspace?access_token=" + Uri.EscapeDataString(ticket["ticket"]!.ToString())), deadline.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, deadline.Token);
        var buffer = new byte[16_384];
        await socket.ReceiveAsync(buffer, deadline.Token);
        app.LinkedIn.SearchPages.Enqueue(new TestProfileSearchPage([Profile()], null));
        (await client.PostAsJsonAsync($"/api/campaigns/{campaign}/search", new { limit = 1 }, deadline.Token)).EnsureSuccessStatusCode();
        var frames = "";
        while (!frames.Contains("WorkspaceNotification"))
        {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            frames += Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        var frame = frames.Split('\u001e', StringSplitOptions.RemoveEmptyEntries).Select(x => JsonNode.Parse(x)!)
            .Single(x => x["target"]?.ToString() == "WorkspaceNotification");
        var notification = frame["arguments"]![0]!;
        Assert.Equal("SearchCompleted", notification["type"]!.ToString());
        Assert.Equal(1, notification["count"]!.GetValue<int>());
        Assert.Equal(automatic, notification["automaticApproval"]!.GetValue<bool>());
        Assert.Equal(campaign.ToString(), notification["campaignId"]!.ToString());
        Assert.Equal("Backend team", notification["campaignName"]!.ToString());
        Assert.DoesNotContain(TestApi.ApiKey, frames);
    }

    [Fact]
    public async Task Campaign_edit_preserves_status_and_requires_a_pause()
    {
        using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, _) = await SetupAsync(client);
        var body = new { name = "Updated role", jobTitle = "Senior engineer", description = "Updated details", requirements = "C#", searchQuery = "C#", location = "Kyiv", locationId = "selected-location-id", recruiter = "Recruiter", minimumFitScore = 0.4m, systemPrompt = "Be concise" };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}", body)).StatusCode);
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Paused" })).EnsureSuccessStatusCode();
        var updated = await JsonAsync(await client.PutAsJsonAsync($"/api/campaigns/{campaign}", body));
        Assert.Equal("Paused", updated["status"]!.ToString());
        Assert.Equal("Updated role", updated["name"]!.ToString());
        Assert.Equal("selected-location-id", updated["locationId"]!.ToString());
        Assert.Equal(0.4m, updated["minimumFitScore"]!.GetValue<decimal>());
        (await client.PutAsJsonAsync($"/api/campaigns/{campaign}/status", new { status = "Stopped" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/campaigns/{campaign}", body)).StatusCode);
    }

    [Fact]
    public async Task Locations_are_cached_case_insensitively_and_validate_query()
    {
        using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var query = "Test" + Guid.NewGuid().ToString("N");
        var first = await JsonAsync(await client.GetAsync($"/api/integrations/linkedin/locations?query={query}"));
        var second = await JsonAsync(await client.GetAsync($"/api/integrations/linkedin/locations?query={query.ToLowerInvariant()}"));
        Assert.Equal(first.ToJsonString(), second.ToJsonString());
        Assert.Single(app.LinkedIn.LocationQueries);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/integrations/linkedin/locations?query=a")).StatusCode);
    }

    [Fact]
    public async Task Realtime_requires_a_ticket_and_pushes_committed_campaign_changes()
    {
        using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/realtime/ticket", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/hubs/workspace?access_token=invalid")).StatusCode);
        var ticket = await JsonAsync(await client.PostAsync("/api/realtime/ticket", null));
        var socketClient = app.Server.CreateWebSocketClient();
        using var socket = await socketClient.ConnectAsync(new Uri("ws://localhost/hubs/workspace?access_token=" + Uri.EscapeDataString(ticket["ticket"]!.ToString())), CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[16_384];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ReceiveAsync(buffer, deadline.Token); // SignalR handshake.
        await SetupAsync(client);
        var message = "";
        while (!message.Contains("WorkspaceChanged"))
        {
            var received = await socket.ReceiveAsync(buffer, deadline.Token);
            message += Encoding.UTF8.GetString(buffer, 0, received.Count);
        }

        Assert.Contains("Campaign", message);
        Assert.DoesNotContain(TestApi.ApiKey, message);
    }
}