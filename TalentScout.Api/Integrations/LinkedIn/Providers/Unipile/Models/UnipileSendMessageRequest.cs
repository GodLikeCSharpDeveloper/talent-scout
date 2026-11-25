namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed record UnipileSendMessageRequest(string AccountId, string RecipientId, string Text, string? ChatId, bool UseInMail);