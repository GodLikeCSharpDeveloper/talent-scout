namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record LinkedInWebhookDefinition(string Name, string Url, LinkedInWebhookKind Kind, string Secret);