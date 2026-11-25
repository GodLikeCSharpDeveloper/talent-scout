using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public string ConnectionString => container.GetConnectionString();

    public Task InitializeAsync()
    {
        return container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return container.DisposeAsync().AsTask();
    }
}

public sealed class TestApi(string connectionString) : WebApplicationFactory<Program>
{
    public const string ApiKey = "integration-tests-only-key-32-characters";
    public TestLinkedIn LinkedIn { get; } = new();
    public TestAi Ai { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Security:ApiKey", ApiKey);
        builder.UseSetting("Pipeline:WorkerEnabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILinkedInGateway>();
            services.RemoveAll<IRecruitmentAi>();
            services.AddSingleton<ILinkedInGateway>(LinkedIn);
            services.AddSingleton<IRecruitmentAi>(Ai);
        });
    }

    public Task<HttpClient> StartAsync()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        return Task.FromResult(client);
    }

    public async Task ClearSendDelayAsync(Guid campaignId)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Campaigns.Where(x => x.Id == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextOutboundAt, (DateTimeOffset?)null));
    }
}

public sealed class TestLinkedIn : ILinkedInGateway
{
    public List<OutgoingMessage> Sent { get; } = [];
    public bool FailAfterSend { get; set; }
    public bool IsConfigured => true;

    public Task<ProfileSearchResult> SearchAsync(ProfileSearch search, CancellationToken ct)
    {
        return Task.FromResult(new ProfileSearchResult([], null));
    }

    public Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        lock (Sent)
            Sent.Add(message);

        if (FailAfterSend)
            throw new TimeoutException("Simulated provider timeout after accepting the message.");

        return Task.FromResult(new SentMessage(Guid.NewGuid().ToString()));
    }
}

public sealed class TestAi : IRecruitmentAi
{
    public Interest Interest { get; set; } = Interest.Interested;
    public decimal Confidence { get; set; } = 0.99m;
    public bool MeetsRequirements { get; set; } = true;

    public Task<ProfileAssessment> AssessProfileAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        return Task.FromResult(new ProfileAssessment(MeetsRequirements, 0.95m, "Relevant experience."));
    }

    public Task<string> WriteIntroductionAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        return Task.FromResult($"Hello {profile.Name}, are you interested in {vacancy.Title}?");
    }

    public Task<ReplyAssessment> ClassifyReplyAsync(Vacancy vacancy, IReadOnlyList<ConversationEntry> history, CancellationToken ct)
    {
        return Task.FromResult(new ReplyAssessment(Interest, Confidence, "Test assessment."));
    }
}