namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record SentMessage(string ExternalId, string? ChatId = null, string? ProviderChatId = null);