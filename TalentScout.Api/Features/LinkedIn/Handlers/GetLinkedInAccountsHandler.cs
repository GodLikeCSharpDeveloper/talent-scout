using Ardalis.Result;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetLinkedInAccountsHandler(ILinkedInAdministration administration)
{
    public async Task<Result<IReadOnlyList<LinkedInAccount>>> HandleAsync(CancellationToken ct)
    {
        var result = new List<LinkedInAccount>();
        var visited = new HashSet<string>();
        string? cursor = null;
        do
        {
            var page = await administration.GetAccountsAsync(cursor, ct);
            result.AddRange(page.Items);
            cursor = page.NextCursor;

            if (!string.IsNullOrEmpty(cursor) && !visited.Add(cursor))
                throw new ApiException(502, "LinkedIn repeated an account-list cursor.");
        } while (!string.IsNullOrEmpty(cursor));

        return Result<IReadOnlyList<LinkedInAccount>>.Success(result);
    }
}