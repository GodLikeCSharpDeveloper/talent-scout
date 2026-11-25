namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;

public interface IUnipileHttpClient
{
    Task<T> GetAsync<T>(string path, CancellationToken ct);
    Task<T> PostAsync<T>(string path, object body, CancellationToken ct);
    Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct);
}