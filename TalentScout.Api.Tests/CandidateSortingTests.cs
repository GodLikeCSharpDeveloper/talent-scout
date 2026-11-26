using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Data.Entities;
using TalentScout.Data.Persistence;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed partial class RecruitmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedIn_status_is_computed_and_sorted_before_pagination(bool descending)
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var campaign = await SeedCandidatePageAsync(app, client);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Enrollments.Include(x => x.Candidate).Where(x => x.CampaignId == campaign).OrderBy(x => x.Candidate.Name).ToListAsync();
            var states = new[] { "Unknown", "Unknown", "Pending", "Connected", "Connected", "Withdrawn", "Declined", "Inactive", "ActionRequired" };
            for (var i = 1; i < states.Length; i++)
            {
                rows[i].HasSentInitialMessage = i == 4;
                db.LinkedInConversations.Add(
                    new LinkedInConversation
                    {
                        EnrollmentId = rows[i].Id, AccountId = "test-account", RecipientId = rows[i].Candidate.ExternalId,
                        ConnectionState = states[i], ConnectionRequestDelivery = i == 1 ? DeliveryStatus.Sending : i is 5 or 6 or 8 ? DeliveryStatus.Unknown : DeliveryStatus.Sent
                    });
            }

            await db.SaveChangesAsync();
        }

        var path = $"/api/campaigns/{campaign}/candidates/page?sortBy=LinkedInStatus&descending={descending}&pageSize=3";
        var ids = new List<string>();
        var statuses = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var response = await JsonAsync(await client.GetAsync(path + $"&page={page}"));
            Assert.Equal(10, response["total"]!.GetValue<int>());
            foreach (var row in response["items"]!.AsArray())
            {
                ids.Add(row!["id"]!.ToString());
                statuses.Add(row["linkedInStatus"]!.ToString());
            }
        }

        var order = new[] { "NotStarted", "Sending", "Pending", "Connected", "Conversation", "Withdrawn", "Declined", "Inactive", "Failed" };
        var ranks = statuses.Select(x => Array.IndexOf(order, x)).ToArray();
        Assert.Equal(descending ? ranks.OrderDescending() : ranks.Order(), ranks);
        Assert.Equal(10, ids.Distinct().Count());
        Assert.All(order, state => Assert.Contains(state, statuses));
    }

    [Fact]
    public async Task Candidate_reads_expose_failure_and_next_attempt_without_changing_the_schedule()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var (campaign, enrollment) = await SetupAsync(client, false);
        var failedAt = new DateTimeOffset(2026, 11, 26, 20, 13, 17, TimeSpan.Zero);
        var nextAt = failedAt.AddHours(6);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Enrollments.FindAsync(enrollment))!.NextContactAt = nextAt;
            db.AuditEvents.Add(new AuditEvent { CampaignId = campaign, EnrollmentId = enrollment, Action = "ProcessingFailed", Details = "ApiException", OccurredAt = failedAt });
            await db.SaveChangesAsync();
        }

        var result = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates/page"));
        var row = Assert.Single(result["items"]!.AsArray())!;
        Assert.Equal(failedAt, DateTimeOffset.Parse(row["lastFailedAttemptAt"]!.ToString()));
        Assert.Equal(nextAt, DateTimeOffset.Parse(row["nextContactAt"]!.ToString()));
        using (var scope = app.Services.CreateScope())
            Assert.Equal(nextAt, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Enrollments.FindAsync(enrollment))!.NextContactAt);
        Assert.Empty(app.LinkedIn.Sent);
        Assert.Empty(app.LinkedIn.Invitations);
    }

    private async Task<Guid> SeedCandidatePageAsync(TestApi app, HttpClient client)
    {
        var (campaign, enrollment) = await SetupAsync(client, false);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db.Enrollments.Include(x => x.Candidate).SingleAsync(x => x.Id == enrollment);
        first.Candidate.Name = "N00";
        first.FitScore = 0;
        first.Status = CandidateStatus.Screened;
        for (var i = 1; i < 10; i++)
        {
            var candidate = new Candidate { Name = $"N{i:00}", ExternalId = Guid.NewGuid().ToString(), ProfileUrl = $"https://www.linkedin.com/in/{Guid.NewGuid()}" };
            db.Candidates.Add(candidate);
            db.Enrollments.Add(
                new Enrollment
                {
                    CampaignId = campaign, Candidate = candidate, CandidateId = candidate.Id,
                    FitScore = i == 9 ? null : i / 10m, Status = i == 9 ? CandidateStatus.Found : i % 2 == 0 ? CandidateStatus.Screened : CandidateStatus.Rejected
                });
        }

        await db.SaveChangesAsync();

        return campaign;
    }

    [Fact]
    public async Task Sorted_page_refetch_places_new_and_moved_candidates_on_their_correct_pages()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var campaign = await SeedCandidatePageAsync(app, client);
        var path = $"/api/campaigns/{campaign}/candidates/page?sortBy=Name&descending=false&page=2&pageSize=3";
        var before = await JsonAsync(await client.GetAsync(path));
        Assert.Equal(new[] { "N03", "N04", "N05" }, before["items"]!.AsArray().Select(x => x!["name"]!.ToString()));
        Guid addedId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var candidate = new Candidate { Name = "N04a", ExternalId = Guid.NewGuid().ToString(), ProfileUrl = $"https://www.linkedin.com/in/{Guid.NewGuid()}" };
            addedId = candidate.Id;
            db.Candidates.Add(candidate);
            db.Enrollments.Add(new Enrollment { CampaignId = campaign, CandidateId = candidate.Id });
            await db.SaveChangesAsync();
        }

        var changed = await JsonAsync(await client.GetAsync(path));
        Assert.Equal(11, changed["total"]!.GetValue<int>());
        Assert.Equal(new[] { "N03", "N04", "N04a" }, changed["items"]!.AsArray().Select(x => x!["name"]!.ToString()));
        var firstPage = await JsonAsync(await client.GetAsync(path.Replace("page=2", "page=1")));
        Assert.DoesNotContain(firstPage["items"]!.AsArray(), x => x!["name"]!.ToString() == "N04a");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Candidates.FindAsync(addedId))!.Name = "Zzz";
            await db.SaveChangesAsync();
        }

        var moved = await JsonAsync(await client.GetAsync(path));
        Assert.Equal(new[] { "N03", "N04", "N05" }, moved["items"]!.AsArray().Select(x => x!["name"]!.ToString()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Score_sort_is_stable_across_pages_and_missing_scores_are_last(bool descending)
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var campaign = await SeedCandidatePageAsync(app, client);
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Enrollments
                .Where(x => x.CampaignId == campaign && x.Candidate.Name == "N02")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FitScore, 0.3m));
        }

        var path = $"/api/campaigns/{campaign}/candidates/page?sortBy=FitScore&descending={descending}&pageSize=3&page=";
        var ids = new List<string>();
        var scores = new List<decimal?>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await JsonAsync(await client.GetAsync(path + page));
            foreach (var row in result["items"]!.AsArray())
            {
                ids.Add(row!["id"]!.ToString());
                scores.Add(row["fitScore"]?.GetValue<decimal>());
            }
        }

        Assert.Equal(10, ids.Distinct().Count());
        Assert.Null(scores.Last());
        var tiedIds = ids.Where((_, index) => scores[index] == 0.3m).ToArray();
        Assert.Equal(2, tiedIds.Length);
        Assert.Equal(tiedIds.Order(StringComparer.Ordinal), tiedIds);
        var scored = scores.Take(9).ToArray();
        Assert.Equal(descending ? scored.OrderDescending() : scored.Order(), scored);
        var clamped = await JsonAsync(await client.GetAsync(path + "999"));
        Assert.Equal(4, clamped["page"]!.GetValue<int>());
        Assert.Single(clamped["items"]!.AsArray());
    }

    [Fact]
    public async Task Filtering_and_total_are_applied_before_pagination_and_unknown_sort_is_rejected()
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var campaign = await SeedCandidatePageAsync(app, client);
        var path = $"/api/campaigns/{campaign}/candidates/page";
        var result = await JsonAsync(await client.GetAsync(path + "?status=Screened&needsDecision=true&search=n0&pageSize=2&sortBy=Name&descending=false"));
        Assert.Equal(5, result["total"]!.GetValue<int>());
        Assert.Equal(new[] { "N00", "N02" }, result["items"]!.AsArray().Select(x => x!["name"]!.ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?sortBy=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?sortBy=untrusted-column")).StatusCode);
    }

    [Theory]
    [InlineData("Status", true)]
    [InlineData("Status", false)]
    [InlineData("FirstContact", true)]
    [InlineData("FirstContact", false)]
    public async Task Stage_and_contact_columns_sort_in_both_directions(string field, bool descending)
    {
        await using var app = new TestApi(database.ConnectionString);
        using var client = await app.StartAsync();
        var campaign = await SeedCandidatePageAsync(app, client);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Enrollments.Where(x => x.CampaignId == campaign).OrderBy(x => x.Candidate.Name).ToListAsync();
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].ApprovalStatus = (OutreachApprovalStatus)(i % 4);
                rows[i].HasSentInitialMessage = i % 5 == 0;
            }

            await db.SaveChangesAsync();
        }

        var result = await JsonAsync(await client.GetAsync($"/api/campaigns/{campaign}/candidates/page?sortBy={field}&descending={descending}&pageSize=100"));
        var ranks = result["items"]!.AsArray().Select(row => field == "Status"
            ? (int)Enum.Parse<CandidateStatus>(row!["status"]!.ToString())
            : row!["hasSentInitialMessage"]!.GetValue<bool>()
                ? 3
                : row["approvalStatus"]!.ToString() switch
                {
                    "Pending" => 0, "Automatic" => 1, "Approved" => 2, _ => 4
                }).ToArray();
        Assert.Equal(descending ? ranks.OrderDescending() : ranks.Order(), ranks);
    }
}