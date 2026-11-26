using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public static class UnipilePagination
{
    public static async Task<IReadOnlyList<T>> ReadAllAsync<T>(Func<string?, CancellationToken, Task<UnipilePage<T>>> readPage, CancellationToken ct)
    {
        var items = new List<T>();
        var seen = new HashSet<string>();
        string? cursor = null;
        for (var page = 0; page < 100; page++)
        {
            var result = await readPage(cursor, ct);

            if (result.Items is null)
                throw new ApiException(502, "Unipile returned an invalid synchronization page.");

            items.AddRange(result.Items);
            cursor = result.Cursor;

            if (string.IsNullOrWhiteSpace(cursor))
                return items;

            if (!seen.Add(cursor))
                break;
        }

        throw new ApiException(502, "LinkedIn synchronization is incomplete. No partial snapshot was applied.");
    }
}