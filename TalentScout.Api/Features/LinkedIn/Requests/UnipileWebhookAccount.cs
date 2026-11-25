using System.Text.Json.Serialization;

namespace TalentScout.Api.Features.LinkedIn.Requests;

public sealed record UnipileWebhookAccount(
    [property: JsonPropertyName("user_id")]
    string UserId);