namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed record UnipilePeopleSearchRequest(
    string AccountId,
    string Keywords,
    IReadOnlyList<string> LocationIds,
    int Limit,
    string? Cursor);