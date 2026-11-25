namespace TalentScout.Api.Features.LinkedIn.Responses;

public sealed record LinkedInIntegrationStatusResponse(
    bool Configured,
    string Provider,
    string ApiVersion,
    string? AccountId,
    bool UseInMail,
    bool WebhookUrlConfigured);