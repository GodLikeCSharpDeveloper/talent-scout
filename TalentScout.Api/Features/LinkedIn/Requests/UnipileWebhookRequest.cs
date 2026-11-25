using System.Text.Json;
using System.Text.Json.Serialization;

namespace TalentScout.Api.Features.LinkedIn.Requests;

public sealed class UnipileWebhookRequest
{
    [JsonPropertyName("user_provider_id")]
    public string? UserProviderId { get; init; }

    [JsonPropertyName("event")]
    public string Event { get; init; } = "";

    [JsonPropertyName("account_type")]
    public string AccountType { get; init; } = "";

    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = "";

    [JsonPropertyName("chat_id")]
    public string ChatId { get; init; } = "";

    [JsonPropertyName("provider_chat_id")]
    public string? ProviderChatId { get; init; }

    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = "";

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("sender")]
    public UnipileWebhookSender? Sender { get; init; }

    [JsonPropertyName("account_info")]
    public UnipileWebhookAccount? AccountInfo { get; init; }

    [JsonPropertyName("is_sender")]
    public JsonElement IsSender { get; init; }

    [JsonPropertyName("is_group")]
    public JsonElement IsGroup { get; init; }

    [JsonPropertyName("is_event")]
    public JsonElement IsEvent { get; init; }

    [JsonPropertyName("attachments")]
    public List<JsonElement>? Attachments { get; init; }
}