using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;

namespace TalentScout.Api.Integrations.Ai.Providers.OpenAi;

public sealed class OpenAiCompletionClient(ChatClient client)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public async Task<T> CompleteAsync<T>(string systemPrompt, string task, object input, ChatResponseFormat format, CancellationToken ct)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(OpenAiPrompts.SystemInstructions)
        ];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new SystemChatMessage(systemPrompt));
        messages.Add(new SystemChatMessage(task));
        messages.Add(new UserChatMessage(JsonSerializer.Serialize(input, JsonOptions)));

        try
        {
            ChatCompletion completion = await client.CompleteChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    ResponseFormat = format,
                    MaxOutputTokenCount = 2000,
                    StoredOutputEnabled = false
                },
                ct);

            if (completion.FinishReason != ChatFinishReason.Stop || !string.IsNullOrWhiteSpace(completion.Refusal))
                throw new ApiException(502, "OpenAI did not return a complete assessment.");

            var text = string.Concat(completion.Content.Select(x => x.Text));

            return JsonSerializer.Deserialize<T>(text, JsonOptions)
                   ?? throw new ApiException(502, "OpenAI returned an empty result.");
        }
        catch (JsonException)
        {
            throw new ApiException(502, "OpenAI returned an invalid structured result.");
        }
        catch (ClientResultException exception)
        {
            var status = exception.Status is 408 or 429 or >= 500 ? 503 : 502;

            throw new ApiException(status, "OpenAI request failed. Check the server configuration or try again later.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(503, "OpenAI request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "OpenAI is temporarily unavailable.");
        }
    }
}