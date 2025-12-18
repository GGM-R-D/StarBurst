using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RNGClient;

public interface IRngClient
{
    Task<PoolsResponse> RequestPoolsAsync(JurisdictionPoolsRequest request, CancellationToken cancellationToken = default);
}

public sealed class RngClient : IRngClient, IDisposable
{
    private static readonly Uri PoolsEndpoint = new("/pools", UriKind.Relative);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public RngClient(RngClientOptions options, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var baseUrl = options.BaseUrl ?? Environment.GetEnvironmentVariable("RNG_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("RNG base URL must be provided via options or RNG_BASE_URL environment variable.");
        }

        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _httpClient.Timeout = options.RequestTimeout ?? TimeSpan.FromSeconds(3);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PoolsResponse> RequestPoolsAsync(JurisdictionPoolsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var payload = JsonSerializer.Serialize(request, SerializerOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, PoolsEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var details = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"RNG service returned {(int)httpResponse.StatusCode}: {details}", null, httpResponse.StatusCode);
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<PoolsResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        return response ?? throw new InvalidOperationException("RNG response payload was empty.");
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}

public sealed record RngClientOptions(string? BaseUrl = null, TimeSpan? RequestTimeout = null);

public sealed class JurisdictionPoolsRequest
{
    public required string GameId { get; init; }
    public required string RoundId { get; init; }
    public required IReadOnlyList<PoolRequest> Pools { get; init; }
    public IDictionary<string, string>? TrackingData { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(GameId))
        {
            throw new ArgumentException("GameId is required.", nameof(GameId));
        }

        if (string.IsNullOrWhiteSpace(RoundId))
        {
            throw new ArgumentException("RoundId is required.", nameof(RoundId));
        }

        if (Pools is null || Pools.Count == 0)
        {
            throw new ArgumentException("At least one pool definition is required.", nameof(Pools));
        }
    }
}

public sealed record PoolRequest(string PoolId, int DrawCount, IReadOnlyDictionary<string, object>? Metadata = null);

public sealed record PoolsResponse(string TransactionId, IReadOnlyList<PoolResult> Pools);

public sealed record PoolResult(string PoolId, IReadOnlyList<string> Results, IDictionary<string, object>? Metadata);
