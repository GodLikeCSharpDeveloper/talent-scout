using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Shared;
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
        builder.UseSetting("LinkedIn:Provider", "Unipile");
        builder.UseSetting("LinkedIn:BaseUrl", "https://unipile.invalid");
        builder.UseSetting("LinkedIn:ApiKey", "test-provider-key");
        builder.UseSetting("LinkedIn:AccountId", "test-account");
        builder.UseSetting("LinkedIn:WebhookSecret", "test-webhook-secret-32-characters-only");
        builder.UseSetting("LinkedIn:WebhookUrl", "https://talentscout.invalid/api/webhooks/unipile");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILinkedInGateway>();
            services.RemoveAll<ILinkedInProfiles>();
            services.RemoveAll<ILinkedInContacts>();
            services.RemoveAll<ILinkedInConversations>();
            services.RemoveAll<ILinkedInSynchronization>();
            services.RemoveAll<ILinkedInSearchParameters>();
            services.RemoveAll<ILinkedInAdministration>();
            services.RemoveAll<IRecruitmentAi>();
            services.AddSingleton<ILinkedInGateway>(LinkedIn);
            services.AddSingleton<ILinkedInProfiles>(LinkedIn);
            services.AddSingleton<ILinkedInContacts>(LinkedIn);
            services.AddSingleton<ILinkedInConversations>(LinkedIn);
            services.AddSingleton<ILinkedInSynchronization>(LinkedIn);
            services.AddSingleton<ILinkedInSearchParameters>(LinkedIn);
            services.AddSingleton<ILinkedInAdministration>(new TestLinkedInAdministration());
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

public sealed class TestLinkedIn : ILinkedInGateway, ILinkedInProfiles, ILinkedInContacts, ILinkedInConversations, ILinkedInSearchParameters, ILinkedInSynchronization
{
    private readonly ConcurrentDictionary<string, CandidateProfile> profiles = new();
    public LinkedInSyncSnapshot? SyncSnapshot { get; set; }
    public bool FailSynchronization { get; set; }
    public List<OutgoingMessage> Sent { get; } = [];
    public Queue<TestProfileSearchPage> SearchPages { get; } = new();
    public List<ProfileSearch> Searches { get; } = [];
    public List<string> LocationQueries { get; } = [];
    public bool FailAfterSend { get; set; }
    public LinkedInContactStatus ContactStatus { get; set; } = new(true, true, false);
    public List<(string RecipientId, string? Note)> Invitations { get; } = [];
    public bool FailAfterInvitation { get; set; }
    public LinkedInChat? Chat { get; set; }

    public Task<LinkedInContactStatus> GetStatusAsync(string recipientId, CancellationToken ct)
    {
        return Task.FromResult(ContactStatus);
    }

    public Task<string> SendInvitationAsync(string recipientId, string? note, CancellationToken ct)
    {
        lock (Invitations)
            Invitations.Add((recipientId, note));

        if (FailAfterInvitation)
            throw new TimeoutException("Invitation accepted but response lost.");

        return Task.FromResult(Guid.NewGuid().ToString());
    }

    public Task<LinkedInChat> GetChatAsync(string chatId, CancellationToken ct)
    {
        if (Chat is not null)
            return Task.FromResult(Chat);

        throw new NotSupportedException("This fake does not model chat metadata.");
    }

    public Task<ProfileSearchResult> SearchAsync(ProfileSearch search, CancellationToken ct)
    {
        Searches.Add(search);

        var page = SearchPages.Count > 0 ? SearchPages.Dequeue() : new TestProfileSearchPage([], null);
        foreach (var profile in page.Profiles)
            profiles[profile.ExternalId] = profile;

        return Task.FromResult(new ProfileSearchResult(page.Profiles.Select(x => new LinkedInProfileReference(x.ExternalId)).ToList(), page.NextCursor));
    }

    public Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        lock (Sent)
            Sent.Add(message);

        if (FailAfterSend)
            throw new TimeoutException("Simulated provider timeout after accepting the message.");

        return Task.FromResult(new SentMessage(Guid.NewGuid().ToString(), message.ChatId ?? Guid.NewGuid().ToString()));
    }

    public Task<CandidateProfile> GetProfileAsync(string identifier, CancellationToken ct)
    {
        return Task.FromResult(profiles[identifier]);
    }

    public Task<IReadOnlyList<LinkedInSearchLocation>> FindLocationsAsync(string query, CancellationToken ct)
    {
        LocationQueries.Add(query);

        return Task.FromResult<IReadOnlyList<LinkedInSearchLocation>>([new LinkedInSearchLocation("test-location", query)]);
    }

    public Task<LinkedInSyncSnapshot> ReadAsync(string recipientId, string? chatId, CancellationToken ct)
    {
        if (FailSynchronization)
            throw new ApiException(503, "Provider unavailable.");

        return Task.FromResult(SyncSnapshot ?? new LinkedInSyncSnapshot(ContactStatus, Chat, []));
    }
}

public sealed class TestAi : IRecruitmentAi
{
    public int AssessmentCalls { get; private set; }
    public int ConnectionNoteCalls { get; private set; }
    public string ConnectionNote { get; set; } = "Hi, I have a developer role that may interest you. Let's connect?";
    public Interest Interest { get; set; } = Interest.Interested;
    public Vacancy? LastVacancy { get; private set; }
    public decimal Confidence { get; set; } = 0.99m;
    public bool MeetsRequirements { get; set; } = true;

    public Task<ProfileAssessment> AssessProfileAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        AssessmentCalls++;
        LastVacancy = vacancy;

        return Task.FromResult(new ProfileAssessment(MeetsRequirements, 0.95m, "Relevant experience."));
    }

    public Task<string> WriteIntroductionAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        LastVacancy = vacancy;

        return Task.FromResult($"Hello {profile.Name}, are you interested in {vacancy.Title}?");
    }

    public Task<string> WriteConnectionNoteAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        LastVacancy = vacancy;
        ConnectionNoteCalls++;

        return Task.FromResult(ConnectionNote);
    }

    public Task<ReplyAssessment> ClassifyReplyAsync(Vacancy vacancy, IReadOnlyList<ConversationEntry> history, CancellationToken ct)
    {
        LastVacancy = vacancy;

        return Task.FromResult(new ReplyAssessment(Interest, Confidence, "Test assessment."));
    }
}