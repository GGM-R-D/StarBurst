using RNGClient;

namespace Simulator;

/// <summary>
/// High-speed local RNG client for simulation purposes.
/// Bypasses network calls by generating random values locally using a thread-safe PRNG.
/// </summary>
public sealed class LocalRngClient : IRngClient
{
    private readonly ThreadLocal<Random> _threadLocalRng;
    private long _transactionCounter;

    public LocalRngClient(int? seed = null)
    {
        // Use a seed-based approach for reproducibility if needed
        var baseSeed = seed ?? Environment.TickCount;
        _threadLocalRng = new ThreadLocal<Random>(() =>
        {
            // Each thread gets a unique seed based on thread ID and base seed
            var threadSeed = baseSeed ^ Environment.CurrentManagedThreadId ^ DateTime.UtcNow.Ticks;
            return new Random((int)(threadSeed & int.MaxValue));
        }, trackAllValues: false);
    }

    public Task<PoolsResponse> RequestPoolsAsync(JurisdictionPoolsRequest request, CancellationToken cancellationToken = default)
    {
        var rng = _threadLocalRng.Value!;
        var pools = new List<PoolResult>();

        foreach (var poolRequest in request.Pools)
        {
            var results = new List<string>(poolRequest.DrawCount);
            
            if (poolRequest.PoolId == "reel-starts" && poolRequest.Metadata != null)
            {
                // Generate reel start positions based on reel lengths
                if (poolRequest.Metadata.TryGetValue("reelLengths", out var reelLengthsObj) && reelLengthsObj is int[] reelLengths)
                {
                    for (int i = 0; i < poolRequest.DrawCount; i++)
                    {
                        var maxValue = i < reelLengths.Length ? reelLengths[i] : 40; // Default to 40 if not specified
                        results.Add(rng.Next(0, maxValue).ToString());
                    }
                }
                else
                {
                    // Fallback: generate random values 0-39 (typical reel length)
                    for (int i = 0; i < poolRequest.DrawCount; i++)
                    {
                        results.Add(rng.Next(0, 40).ToString());
                    }
                }
            }
            else if (poolRequest.PoolId == "multiplier-seeds")
            {
                // Generate random seeds for multiplier selection
                for (int i = 0; i < poolRequest.DrawCount; i++)
                {
                    results.Add(rng.Next(0, int.MaxValue).ToString());
                }
            }
            else
            {
                // Generic random values
                for (int i = 0; i < poolRequest.DrawCount; i++)
                {
                    results.Add(rng.Next(0, int.MaxValue).ToString());
                }
            }

            pools.Add(new PoolResult(poolRequest.PoolId, results, poolRequest.Metadata as IDictionary<string, object>));
        }

        var transactionId = $"SIM-{Interlocked.Increment(ref _transactionCounter):X16}";
        return Task.FromResult(new PoolsResponse(transactionId, pools));
    }
}
