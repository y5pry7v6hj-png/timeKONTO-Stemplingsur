using System.Net.Http.Json;
using System.Text.Json;
using Stemplingsur.Models;

namespace Stemplingsur.Services;

public sealed class ApiStemplingService : IStemplingService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiStemplingService(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<Employee>>("employees", cancellationToken) ?? [];

    public async Task<IReadOnlyList<TimeEntry>> GetActiveEntriesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<TimeEntry>>("active", cancellationToken) ?? [];

    public Task<TimeEntry> ClockInAsync(ClockInRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ClockInRequest, TimeEntry>("clock-in", request, cancellationToken);

    public async Task ClockOutAsync(ClockOutRequest request, CancellationToken cancellationToken = default) =>
        await PostAsync<ClockOutRequest, ApiOk>("clock-out", request, cancellationToken);

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default) =>
        _ = await GetAsync<List<Employee>>("employees", cancellationToken);

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string action)
    {
        var baseUrl = ApiSettings.BaseUrl.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("API-adressen er ikke gyldig.");

        var separator = baseUrl.Contains('?') ? "&" : "?";
        var request = new HttpRequestMessage(method, new Uri(baseUrl + separator + "action=" + Uri.EscapeDataString(action)));
        var apiKey = await ApiSettings.GetApiKeyAsync();
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-API-Key", apiKey);
        return request;
    }

    private async Task<T?> GetAsync<T>(string action, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, action);
        using var response = await _http.SendAsync(request, cancellationToken);
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string action, TRequest body, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, action);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await _http.SendAsync(request, cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("Tomt svar fra serveren.");
    }

    private static async Task<T?> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = JsonSerializer.Deserialize<ApiError>(payload, JsonOptions);
                throw new InvalidOperationException(error?.Error ?? $"Serverfeil {(int)response.StatusCode}.");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"Serverfeil {(int)response.StatusCode}.");
            }
        }
        if (string.IsNullOrWhiteSpace(payload)) return default;
        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private sealed record ApiError(string? Error);
    private sealed record ApiOk(bool Ok);
}
