using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TalentScout.Api.Features.Candidates.Models;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class SearchCandidatePageHandler(
    ILinkedInGateway gateway,
    ILinkedInSearchParameters parameters,
    IDataProtectionProvider protection,
    IOptions<LinkedInOptions> options)
{
    public async Task<ProfileSearchResult> HandleAsync(string query, string location, int limit, string? cursor, CancellationToken ct, string? locationId = null)
    {
        var protector = protection.CreateProtector("TalentScout.CandidateSearch.v1");
        CandidateSearchCursor? state = null;
        if (!string.IsNullOrEmpty(cursor))
        {
            try
            {
                state = JsonSerializer.Deserialize<CandidateSearchCursor>(protector.Unprotect(cursor));
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                throw new ApiException(400, "Invalid or expired search cursor. Start a new search.");
            }

            if (state is null || state.AccountId != options.Value.AccountId || state.Query != query || state.Location != location ||
                state.ExpiresAt < DateTimeOffset.UtcNow || state.RemainingProfiles is null || state.LocationIds is null || (locationId is not null && !state.LocationIds.SequenceEqual(new[] { locationId })))
                throw new ApiException(400, "The cursor does not match this search or has expired.");
        }

        var locationIds = state?.LocationIds ?? [];
        if (state is null && !string.IsNullOrWhiteSpace(locationId))
            locationIds = [locationId];
        else if (state is null && !string.IsNullOrWhiteSpace(location))
        {
            var locations = await parameters.FindLocationsAsync(location, ct);
            var selected = locations.FirstOrDefault(x => string.Equals(x.Name, location, StringComparison.OrdinalIgnoreCase))
                           ?? locations.FirstOrDefault();

            if (selected is null)
                throw new ApiException(400, "LinkedIn could not resolve the campaign location. Use a supported country or city.");

            locationIds = [selected.Id];
        }

        var profiles = state?.RemainingProfiles ?? [];
        var providerCursor = state?.ProviderCursor;
        if (profiles.Count == 0)
        {
            var page = await gateway.SearchAsync(new ProfileSearch(query, locationIds, limit, providerCursor), ct);

            if (page is null || page.Profiles is null || page.Profiles.Count > 100 ||
                page.Profiles.Any(x => string.IsNullOrWhiteSpace(x.ExternalId)))
                throw new ApiException(502, "LinkedIn returned an invalid search page.");

            if (providerCursor is not null && page.NextCursor == providerCursor)
                return new ProfileSearchResult(page.Profiles.Take(limit).ToList(), cursor);

            profiles = page.Profiles;
            providerCursor = page.NextCursor;
        }

        var selectedProfiles = profiles.Take(limit).ToList();
        var remaining = profiles.Skip(selectedProfiles.Count).ToList();
        var next = remaining.Count == 0 && string.IsNullOrEmpty(providerCursor)
            ? null
            : protector.Protect(
                JsonSerializer.Serialize(
                    new CandidateSearchCursor(
                        options.Value.AccountId,
                        query,
                        location,
                        locationIds,
                        providerCursor,
                        remaining,
                        state?.ExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30))));

        return new ProfileSearchResult(selectedProfiles, next);
    }
}