using System.Text.Json.Serialization;

namespace TalentScout.Api.Features.LinkedIn.Requests;

public sealed record UnipileWebhookSender(
    [property: JsonPropertyName("attendee_provider_id")]
    string ProviderId);