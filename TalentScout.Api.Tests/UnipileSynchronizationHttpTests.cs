using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;
using TalentScout.Api.Shared;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed class UnipileSynchronizationHttpTests
{
    [Theory]
    [InlineData(422, "{\"type\":\"errors/cannot_resend\",\"title\":\"Invitation restricted\",\"detail\":\"Wait before resending.\"}", 409, "errors/cannot_resend")]
    [InlineData(422, "<html>upstream error</html>", 409, "Check account connection")]
    [InlineData(429, "{}", 503, "Check account connection")]
    public async Task Provider_failure_preserves_HTTP_status_and_safe_error_details(int providerStatus, string body, int expectedStatus, string expectedReason)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapPost("/api/v1/users/invite", () => Results.Text(body, "application/json", statusCode: providerStatus));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address + "/api/v1/") };
        var api = new UnipileProfilesApi(new UnipileHttpClient(http));
        var error = await Assert.ThrowsAsync<ApiException>(() => api.InviteAsync("account", "recipient", "Hello", CancellationToken.None));
        Assert.Equal(expectedStatus, error.Status);
        Assert.Contains($"HTTP {providerStatus}", error.Message);
        Assert.Contains(expectedReason, error.Message);
        Assert.DoesNotContain("<html>", error.Message);
    }

    [Theory]
    [InlineData("IGNORED", "SENT", ConnectionInvitationStatus.Declined)]
    [InlineData("WITHDRAWN", "SENT", ConnectionInvitationStatus.Withdrawn)]
    [InlineData("IGNORED", "RECEIVED", null)]
    [InlineData("NEW_PROVIDER_STATUS", "SENT", null)]
    [InlineData("PENDING", "SENT", ConnectionInvitationStatus.Pending)]
    public async Task Profile_adapter_preserves_the_reason_only_for_our_sent_invitation(string status, string type, ConnectionInvitationStatus? expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/api/v1/users/recipient", () => Results.Json(new { invitation = new { type, status } }));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address + "/api/v1/") };
        var adapter = new UnipileContactsAdapter(
            new UnipileProfilesApi(new UnipileHttpClient(http)),
            Options.Create(new LinkedInOptions { AccountId = "account" }));
        var result = await adapter.GetStatusAsync("recipient", CancellationToken.None);
        Assert.Equal(expected, result.SentInvitationStatus);
        Assert.Equal(expected == ConnectionInvitationStatus.Pending, result.InvitationPending);
    }

    [Fact]
    public async Task Typed_HTTP_methods_follow_invitation_pagination_and_parse_message_flags()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var invitationPages = 0;
        app.MapGet(
            "/api/v1/users/invite/sent",
            (HttpRequest request) =>
            {
                Assert.Equal("account", request.Query["account_id"].ToString());
                invitationPages++;

                return request.Query["cursor"] == "second"
                    ? Results.Json(new { items = new[] { new { id = "invitation", invited_user_id = "recipient", invitation_text = "Let's connect" } }, cursor = (string?)null })
                    : Results.Json(new { items = Array.Empty<object>(), cursor = "second" });
            });
        app.MapGet("/api/v1/chat_attendees/recipient/chats", () => Results.Json(new { items = new[] { new { id = "chat", account_id = "account" } }, cursor = (string?)null }));
        app.MapGet("/api/v1/chats/chat", () => Results.Json(new { id = "chat", account_id = "account", provider_id = "provider-chat", attendee_provider_id = "recipient" }));
        app.MapGet(
            "/api/v1/chats/chat/messages",
            () => Results.Json(
                new
                {
                    items = new[]
                    {
                        new { id = "message", chat_id = "chat", account_id = "account", text = "Manual message", timestamp = "2026-11-26T20:13:17Z", is_sender = 1, edited = 1, deleted = 0 }
                    },
                    cursor = (string?)null
                }));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(address + "/api/v1/") };
        var transport = new UnipileHttpClient(http);
        var options = Options.Create(new LinkedInOptions { AccountId = "account" });
        var messaging = new UnipileMessagingApi(transport);
        var adapter = new UnipileSynchronizationAdapter(
            new UnipileProfilesApi(transport),
            messaging,
            new Contacts(),
            new UnipileConversationsAdapter(messaging, options),
            options);
        var result = await adapter.ReadAsync("recipient", null, CancellationToken.None);
        Assert.Equal(2, invitationPages);
        Assert.True(result.Contact.InvitationPending);
        Assert.Equal("invitation", result.PendingInvitationId);
        Assert.Equal("Let's connect", result.InvitationNote);
        var message = Assert.Single(result.Messages);
        Assert.True(message.IsOutgoing);
        Assert.True(message.IsEdited);
        Assert.False(message.IsDeleted);
        Assert.Equal("unipile:account:message", message.ExternalId);
    }

    [Fact]
    public async Task Repeated_cursor_fails_instead_of_applying_an_incomplete_snapshot()
    {
        var calls = 0;
        var exception = await Assert.ThrowsAsync<ApiException>(() => UnipilePagination.ReadAllAsync(
            (_, _) =>
            {
                calls++;

                return Task.FromResult(new UnipilePage<int>([1], "repeated"));
            },
            CancellationToken.None));
        Assert.Equal(502, exception.Status);
        Assert.Equal(2, calls);
    }

    private sealed class Contacts : ILinkedInContacts
    {
        public Task<LinkedInContactStatus> GetStatusAsync(string recipientId, CancellationToken ct)
        {
            return Task.FromResult(new LinkedInContactStatus(false, false, false));
        }

        public Task<string> SendInvitationAsync(string recipientId, string? note, CancellationToken ct)
        {
            throw new InvalidOperationException("Read synchronization must not send invitations.");
        }
    }
}