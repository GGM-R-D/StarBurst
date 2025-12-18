using GameEngine.Services;
using RNGClient;

namespace GameEngineHost.Services;

/// <summary>
/// Integrated RNG client that generates random numbers locally using Fortuna algorithm.
/// Replaces the external RngHost service and implements jurisdiction-compliant RNG.
/// </summary>
public sealed class IntegratedRngClient : IRngClient, IDisposable
{
    private readonly FortunaPrng _fortunaPrng;

    public IntegratedRngClient(FortunaPrng fortunaPrng)
    {
        _fortunaPrng = fortunaPrng;
    }

    public Task<PoolsResponse> RequestPoolsAsync(JurisdictionPoolsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        // Use Fortuna PRNG for cryptographically secure random number generation
        // This ensures compliance with jurisdiction requirements
        var pools = request.Pools.Select(pool =>
        {
            var results = Enumerable.Range(0, pool.DrawCount)
                .Select(_ => _fortunaPrng.NextInt32(0, int.MaxValue).ToString())
                .ToArray();
            // Convert IReadOnlyDictionary to IDictionary for PoolResult constructor
            IDictionary<string, object>? metadata = pool.Metadata != null
                ? new Dictionary<string, object>(pool.Metadata)
                : null;
            return new PoolResult(pool.PoolId, results, metadata);
        }).ToList();

        // Use roundId as transaction ID if provided, otherwise generate one
        // This ensures proper tracking for compliance
        var transactionId = !string.IsNullOrWhiteSpace(request.RoundId)
            ? request.RoundId
            : Guid.NewGuid().ToString("N");
        
        var response = new PoolsResponse(transactionId, pools);

        return Task.FromResult(response);
    }

    public void Dispose()
    {
        // FortunaPrng is managed by DI, don't dispose here
    }
}

