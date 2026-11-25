using System.Text.Json;
using System.Text.Json.Serialization;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;

public sealed class UnipileHttpClient(HttpClient http) : IUnipileHttpClient
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        return SendAsync<T>(HttpMethod.Get, path, null, ct);
    }

    public Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        return SendAsync<T>(HttpMethod.Post, path, JsonContent.Create(body, options: JsonOptions), ct);
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        if (http.BaseAddress is null)
            throw new ApiException(503, "Unipile is not configured.");

        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;

                throw new ApiException(
                    status is 408 or 429 or >= 500 ? 503 : status == 404 ? 404 : 409,
                    $"Unipile rejected the operation (HTTP {status}). Check account connection, permissions and provider limits.");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                   ?? throw new ApiException(502, "Unipile returned an empty response.");
        }
        catch (JsonException)
        {
            throw new ApiException(502, "Unipile returned an invalid response.");
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "Unipile could not be reached.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(503, "Unipile request timed out.");
        }
    }
}