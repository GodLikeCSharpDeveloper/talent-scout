namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record OutgoingMessage(string RecipientId, string Text, string IdempotencyKey, string? ChatId = null);