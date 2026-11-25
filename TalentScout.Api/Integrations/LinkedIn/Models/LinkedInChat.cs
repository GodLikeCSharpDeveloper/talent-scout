namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record LinkedInChat(string AccountId, string ChatId, string? ProviderChatId, string? RecipientId);