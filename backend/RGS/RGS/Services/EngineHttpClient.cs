using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using GameEngine.Play;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RGS.Services;

public interface IEngineClient
{
    Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken);
}

public sealed class EngineCallException : Exception
{
    public int HttpStatusCode { get; }
    public string? ErrorContent { get; }

    public EngineCallException(int httpStatusCode, string? errorContent)
        : base($"Backend engine returned {(HttpStatusCode)httpStatusCode} - {errorContent}")
    {
        HttpStatusCode = httpStatusCode;
        ErrorContent = errorContent;
    }
}

public sealed class EngineHttpClient : IEngineClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<EngineHttpClient> _logger;

    public EngineHttpClient(HttpClient httpClient, IOptions<JsonOptions> jsonOptions, ILogger<EngineHttpClient> logger)
    {
        _httpClient = httpClient;
        _serializerOptions = jsonOptions.Value.SerializerOptions;
        _logger = logger;
    }

    public async Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling backend engine at {BaseAddress}/play with GameId={GameId}, TotalBet={TotalBet}", 
            _httpClient.BaseAddress, request.GameId, request.TotalBet.Amount);
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/play", request, _serializerOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Backend engine returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new EngineCallException((int)response.StatusCode, errorContent);
            }

            var payload = await response.Content.ReadFromJsonAsync<PlayResponse>(_serializerOptions, cancellationToken: cancellationToken);
            
            if (payload == null)
            {
                _logger.LogError("Backend engine returned empty response");
                throw new InvalidOperationException("Engine response payload was empty.");
            }
            
            _logger.LogInformation("Backend engine response received: Win={Win}, RoundId={RoundId}", 
                payload.Win.Amount, payload.RoundId);
            
            return payload;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling backend engine");
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout calling backend engine");
            throw new HttpRequestException("Backend engine request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling backend engine");
            throw;
        }
    }
}

