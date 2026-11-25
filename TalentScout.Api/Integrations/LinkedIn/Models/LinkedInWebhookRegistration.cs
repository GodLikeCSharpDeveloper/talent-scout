namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record LinkedInWebhookRegistration(string Id, string Url, bool AlreadyRegistered, string? RelationWebhookId = null);