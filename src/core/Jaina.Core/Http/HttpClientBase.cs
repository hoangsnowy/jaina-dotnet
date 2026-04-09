using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Jaina.Core.Http;

public abstract class HttpClientBase
{
    private readonly HttpClient _httpClient;

    protected HttpClientBase(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    protected async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(
#if NET8_0_OR_GREATER
            ct
#endif
        ).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(content, JsonSerializerDefaults.Options);
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, JsonSerializerDefaults.Options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(
#if NET8_0_OR_GREATER
            ct
#endif
        ).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonSerializerDefaults.Options);
    }

    protected async Task PostAsync<TRequest>(string url, TRequest data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, JsonSerializerDefaults.Options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    protected void SetBearerToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    protected void SetHeader(string name, string value)
    {
        _httpClient.DefaultRequestHeaders.Remove(name);
        _httpClient.DefaultRequestHeaders.Add(name, value);
    }
}
