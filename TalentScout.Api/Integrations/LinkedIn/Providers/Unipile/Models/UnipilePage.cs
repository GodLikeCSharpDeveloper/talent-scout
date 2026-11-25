namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed record UnipilePage<T>(List<T>? Items, string? Cursor);