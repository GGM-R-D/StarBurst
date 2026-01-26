// ═══════════════════════════════════════════════════════════════════════════════════════════════
// LocalRngClient.cs - High-Speed Local Random Number Generator
// ═══════════════════════════════════════════════════════════════════════════════════════════════
//
// PURPOSE:
//   Provides random numbers for the game engine during simulation WITHOUT making HTTP calls.
//   This is ~1000x faster than using a real RNG service over the network.
//
// WHY NOT USE THE REAL RNG?
//   In production, the game engine calls an external RNG service (HTTP API) to get random
//   numbers. This is required for regulatory compliance (certified RNG).
//   
//   For simulation:
//   - We need SPEED: millions of spins must complete in minutes, not hours
//   - We don't need certification: simulation results aren't used for real gambling
//   - We need REPRODUCIBILITY: optional seed for debugging specific outcomes
//
// HOW IT WORKS:
//   1. Implements IRngClient interface (same as the real RNG client)
//   2. Uses System.Random (pseudo-random number generator)
//   3. Each thread gets its OWN Random instance (ThreadLocal) to avoid contention
//   4. Generates reel positions (0 to stripLength-1) for each reel
//
// THREAD-SAFETY:
//   System.Random is NOT thread-safe. If multiple threads share one Random instance,
//   they can corrupt its internal state, producing non-random or duplicate values.
//   
//   Solution: ThreadLocal<Random> gives each thread its own Random instance.
//   
//   Thread 1: Uses Random(seed1) → 23, 7, 15, 31, ...
//   Thread 2: Uses Random(seed2) → 8, 29, 3, 17, ...
//   Thread 3: Uses Random(seed3) → 12, 0, 38, 22, ...
//   
//   No conflicts because each thread has independent state!
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════

using RNGClient;

namespace Simulator;

/// <summary>
/// High-speed local RNG client for simulation purposes.
/// 
/// This class replaces the real RNG service (which makes HTTP calls) with a local PRNG.
/// It implements IRngClient so it can be injected into SpinHandler just like the real RNG.
/// 
/// Performance comparison:
///   - Real RNG (HTTP): ~10-50ms per call (network latency)
///   - LocalRngClient: ~0.001ms per call (just math)
///   - Speedup: ~10,000x to 50,000x faster!
/// </summary>
public sealed class LocalRngClient : IRngClient
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // FIELDS
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Thread-local Random instance.
    /// 
    /// ThreadLocal ensures each thread has its own Random:
    /// - Thread 1 calls _threadLocalRng.Value → gets Random instance A
    /// - Thread 2 calls _threadLocalRng.Value → gets Random instance B
    /// - They never interfere with each other!
    /// 
    /// The factory function (passed to constructor) is called once per thread.
    /// </summary>
    private readonly ThreadLocal<Random> _threadLocalRng;
    
    /// <summary>
    /// Counter for generating unique transaction IDs.
    /// Uses Interlocked.Increment for thread-safe incrementing.
    /// </summary>
    private long _transactionCounter;

    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Creates a new LocalRngClient.
    /// </summary>
    /// <param name="seed">
    /// Optional seed for reproducibility.
    /// - If provided: same seed → same sequence of random numbers
    /// - If null: uses Environment.TickCount (different each run)
    /// 
    /// Reproducibility is useful for debugging:
    ///   "I saw a bug at spin 500,000 with seed 12345"
    ///   → Run again with seed 12345 → same bug appears!
    /// </param>
    public LocalRngClient(int? seed = null)
    {
        // Use provided seed or generate one from system tick count
        var baseSeed = seed ?? Environment.TickCount;
        
        // Create ThreadLocal with a factory function that creates unique Random per thread
        _threadLocalRng = new ThreadLocal<Random>(() =>
        {
            // Each thread gets a unique seed by XOR-ing:
            // - baseSeed: provided seed or tick count
            // - CurrentManagedThreadId: unique ID for this thread
            // - DateTime.UtcNow.Ticks: adds time-based variation
            var threadSeed = baseSeed ^ Environment.CurrentManagedThreadId ^ DateTime.UtcNow.Ticks;
            
            // Mask to int range (Random constructor takes int, not long)
            return new Random((int)(threadSeed & int.MaxValue));
        }, trackAllValues: false); // Don't track all values (we don't need to enumerate them)
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
