using System.Text.Json.Nodes;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalentScout.Api.Features.LinkedIn;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Validators;
using TalentScout.Api.Integrations.LinkedIn;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed class LinkedInWebhookHttpTests
{
    [Fact]
    public async Task Startup_uses_real_http_adapter_and_restart_does_not_create_duplicate_webhooks()
    {
        const string callback = "https://talentscout.invalid/api/webhooks/unipile";
        const string secret = "http-tests-webhook-secret-32-characters";
        var created = new List<JsonNode>();
        var keys = new List<string>();
        var listCalls = 0;
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var provider = builder.Build();
        provider.MapGet(
            "/api/v1/webhooks",
            (HttpContext context) =>
            {
                listCalls++;
                keys.Add(context.Request.Headers["X-API-KEY"].ToString());

                return Results.Json(
                    new
                    {
                        @object = "WebhookList",
                        items = created.Select((x, i) => new { id = "webhook-" + i, name = x["name"]!.ToString(), request_url = x["request_url"]!.ToString() }),
                        cursor = (string?)null
                    });
            });
        provider.MapPost(
            "/api/v1/webhooks",
            async (HttpContext context) =>
            {
                keys.Add(context.Request.Headers["X-API-KEY"].ToString());
                created.Add((await context.Request.ReadFromJsonAsync<JsonNode>())!);

                return Results.Json(new { @object = "WebhookCreated", webhook_id = "webhook-" + (created.Count - 1) }, statusCode: 201);
            });
        await provider.StartAsync();
        var providerUrl = provider.Urls.Single();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LinkedIn:Provider"] = "Unipile",
                ["LinkedIn:BaseUrl"] = "https://unipile.invalid",
                ["LinkedIn:ApiKey"] = "test-unipile-key",
                ["LinkedIn:AccountId"] = "account-1",
                ["LinkedIn:WebhookSecret"] = secret,
                ["LinkedIn:WebhookUrl"] = callback
            }).Build();

        for (var launch = 0; launch < 2; launch++)
        {
            using var host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddLinkedInIntegration(configuration);
                services.AddHttpClient<IUnipileHttpClient, UnipileHttpClient>()
                    .ConfigureHttpClient(http => http.BaseAddress = new Uri(providerUrl + "/api/v1/"));
                services.AddScoped<RegisterLinkedInWebhooksHandler>();
                services.AddScoped<IValidator<RegisterLinkedInWebhooksRequest>, RegisterLinkedInWebhooksValidator>();
                services.AddHostedService<LinkedInWebhookStartupService>();
            }).Build();
            await host.StartAsync();
            await host.StopAsync();
        }

        Assert.Equal(2, created.Count);
        Assert.Equal(2, listCalls);
        Assert.All(keys, key => Assert.Equal("test-unipile-key", key));
        Assert.Collection(
            created,
            messages =>
            {
                Assert.Equal("messaging", messages["source"]!.ToString());
                Assert.Equal("message_received", messages["events"]![0]!.ToString());
            },
            relations =>
            {
                Assert.Equal("users", relations["source"]!.ToString());
                Assert.Equal("new_relation", relations["events"]![0]!.ToString());
            });
        Assert.All(
            created,
            request =>
            {
                Assert.Equal(callback, request["request_url"]!.ToString());
                Assert.Equal("account-1", request["account_ids"]![0]!.ToString());
                Assert.Equal("json", request["format"]!.ToString());
                Assert.True(request["enabled"]!.GetValue<bool>());
                Assert.Equal("X-Unipile-Secret", request["headers"]![0]!["key"]!.ToString());
                Assert.Equal(secret, request["headers"]![0]!["value"]!.ToString());
            });
    }
}