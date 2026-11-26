using System.Text.Json;
using System.Text.Json.Serialization;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed class UnipileChatMessage
{
    public string Id { get; init; } = "";
    public string ChatId { get; init; } = "";
    public string? AccountId { get; init; }
    public string? Text { get; init; }
    public DateTimeOffset? Timestamp { get; init; }

    [JsonConverter(typeof(UnipileBooleanConverter))]
    public bool? IsSender { get; init; }

    [JsonConverter(typeof(UnipileBooleanConverter))]
    public bool? Deleted { get; init; }

    [JsonConverter(typeof(UnipileBooleanConverter))]
    public bool? Edited { get; init; }

    [JsonConverter(typeof(UnipileBooleanConverter))]
    public bool? IsEvent { get; init; }

    [JsonConverter(typeof(UnipileBooleanConverter))]
    public bool? Hidden { get; init; }

    public List<JsonElement>? Attachments { get; init; }
}

// Unipile v1 uses both JSON booleans and 0/1 flags across providers.
public sealed class UnipileBooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt32(out var number) && number is 0 or 1 => number == 1,
            JsonTokenType.String when reader.GetString() is "true" or "1" => true,
            JsonTokenType.String when reader.GetString() is "false" or "0" => false,
            _ => throw new JsonException("Invalid Unipile boolean flag.")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteBooleanValue(value.Value);
        else
            writer.WriteNullValue();
    }
}