using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ardalis.Result;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetLinkedInLocationsHandler(ILocationCacheRepository cache, ILinkedInSearchParameters parameters, IOptions<LinkedInOptions> options)
{
    public async Task<Result<List<LinkedInSearchLocation>>> HandleAsync(string query, CancellationToken ct)
    {
        query = query.Trim();

        if (query.Length is < 2 or > 100)
            return Result<List<LinkedInSearchLocation>>.Invalid(new ValidationError("query", "Enter 2–100 characters."));
        if (!options.Value.IsConfigured)
            return Result<List<LinkedInSearchLocation>>.Unavailable("LinkedIn is not configured.");

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{options.Value.AccountId}\n{query.ToUpperInvariant()}")));
        var existing = await cache.FindAsync(key, ct);

        if (existing is not null && existing.UpdatedAt > DateTimeOffset.UtcNow.AddDays(-7))
            return Result<List<LinkedInSearchLocation>>.Success(JsonSerializer.Deserialize<List<LinkedInSearchLocation>>(existing.ResultsJson) ?? []);

        var results = (await parameters.FindLocationsAsync(query, ct)).Take(100).ToList();
        await cache.StoreAsync(new LinkedInLocationCache { Id = key, ResultsJson = JsonSerializer.Serialize(results), UpdatedAt = DateTimeOffset.UtcNow }, ct);

        return Result<List<LinkedInSearchLocation>>.Success(results);
    }
}