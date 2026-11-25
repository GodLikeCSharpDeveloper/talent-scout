using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TalentScout.Api.Features.LinkedIn;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Validators;
using TalentScout.Api.Integrations.LinkedIn;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Shared;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed class LinkedInWebhookStartupTests
{
    private const string Url = "https://talentscout.invalid/api/webhooks/unipile";
    private const string Secret = "startup-tests-webhook-secret-32-characters";

    private static IHost CreateHost(
        TestLinkedInAdministration administration,
        string? key = "test-key",
        string provider = "Unipile",
        string url = Url,
        StartupProbe? probe = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LinkedIn:Provider"] = provider,
                ["LinkedIn:BaseUrl"] = "https://unipile.invalid",
                ["LinkedIn:ApiKey"] = key,
                ["LinkedIn:AccountId"] = "account-1",
                ["LinkedIn:WebhookSecret"] = Secret,
                ["LinkedIn:WebhookUrl"] = url
            }).Build();

        return new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddLinkedInIntegration(configuration);
            services.AddSingleton<ILinkedInAdministration>(administration);
            services.AddScoped<RegisterLinkedInWebhooksHandler>();
            services.AddScoped<IValidator<RegisterLinkedInWebhooksRequest>, RegisterLinkedInWebhooksValidator>();
            services.AddHostedService<LinkedInWebhookStartupService>();
            if (probe is not null)
                services.AddSingleton<IHostedService>(probe);
        }).Build();
    }

    [Fact]
    public async Task Startup_registers_both_subscriptions_before_other_workers()
    {
        var administration = new TestLinkedInAdministration();
        var probe = new StartupProbe(() => Assert.Equal(2, administration.Created.Count));
        using var host = CreateHost(administration, probe: probe);

        await host.StartAsync();

        Assert.True(probe.Started);
        Assert.Collection(
            administration.Created,
            messages => Assert.Equal(LinkedInWebhookKind.Messages, messages.Kind),
            connections => Assert.Equal(LinkedInWebhookKind.Connections, connections.Kind));
        Assert.All(
            administration.Created,
            x =>
            {
                Assert.Equal(Url, x.Url);
                Assert.Equal(Secret, x.Secret);
                Assert.StartsWith("TalentScout/account-1/", x.Name);
            });
        await host.StopAsync();
    }

    [Fact]
    public async Task Restart_reuses_existing_subscriptions_including_paginated_results()
    {
        var administration = new TestLinkedInAdministration { PageSize = 1 };
        administration.Webhooks.Add(new LinkedInWebhook("other", "Another application", "https://other.invalid/hook"));
        using (var first = CreateHost(administration))
        {
            await first.StartAsync();
            await first.StopAsync();
        }

        using (var restarted = CreateHost(administration))
        {
            await restarted.StartAsync();
            await restarted.StopAsync();
        }

        Assert.Equal(2, administration.Created.Count);
        Assert.Equal(3, administration.Webhooks.Count);
        Assert.Equal(4, administration.ListCalls);
    }

    [Theory]
    [InlineData(null, "Unipile")]
    [InlineData("", "Unipile")]
    [InlineData("test-key", "Disabled")]
    public async Task Unconfigured_integration_starts_without_contacting_the_provider(string? key, string provider)
    {
        var administration = new TestLinkedInAdministration();
        var probe = new StartupProbe();
        using var host = CreateHost(administration, key, provider, "", probe);

        await host.StartAsync();

        Assert.True(probe.Started);
        Assert.Equal(0, administration.ListCalls);
        Assert.Empty(administration.Created);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://talentscout.invalid/api/webhooks/unipile")]
    [InlineData("https://talentscout.invalid/wrong-path")]
    public async Task Invalid_callback_configuration_prevents_workers_from_starting(string url)
    {
        var administration = new TestLinkedInAdministration();
        var probe = new StartupProbe();
        using var host = CreateHost(administration, url: url, probe: probe);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.False(probe.Started);
        Assert.Equal(0, administration.ListCalls);
        Assert.Empty(administration.Created);
    }

    [Fact]
    public async Task Partial_provider_failure_stops_startup_and_restart_creates_only_missing_subscription()
    {
        var administration = new TestLinkedInAdministration { FailNextCreation = LinkedInWebhookKind.Connections };
        var probe = new StartupProbe();
        using (var failed = CreateHost(administration, probe: probe))
            await Assert.ThrowsAsync<HttpRequestException>(() => failed.StartAsync());
        Assert.False(probe.Started);
        Assert.Single(administration.Created);

        using var restarted = CreateHost(administration);
        await restarted.StartAsync();
        Assert.Equal(2, administration.Created.Count);
        await restarted.StopAsync();
    }

    [Fact]
    public async Task Repeated_provider_cursor_fails_startup_instead_of_looping_forever()
    {
        var administration = new TestLinkedInAdministration { RepeatCursor = true };
        using var host = CreateHost(administration);

        var exception = await Assert.ThrowsAsync<ApiException>(() => host.StartAsync());

        Assert.Equal(502, exception.Status);
        Assert.Equal(2, administration.ListCalls);
        Assert.Empty(administration.Created);
    }

    [Fact]
    public async Task Startup_cancellation_is_propagated_to_provider_operations()
    {
        var administration = new TestLinkedInAdministration { BlockRead = true };
        using var host = CreateHost(administration);
        using var cancellation = new CancellationTokenSource();
        var start = host.StartAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.Empty(administration.Created);
    }

    private sealed class StartupProbe(Action? onStart = null) : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken ct)
        {
            onStart?.Invoke();
            Started = true;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}