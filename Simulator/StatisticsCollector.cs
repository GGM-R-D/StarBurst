using System.Collections.Concurrent;

namespace Simulator;

/// <summary>
/// Thread-safe statistics collector for GLI compliance metrics.
/// Tracks RTP, hit frequency, payline reachability, multiplier distribution, and feature frequency.
/// </summary>
public sealed class StatisticsCollector
{
    // Core metrics (using long for atomic operations)
    private long _totalSpins;
    private long _winningSpins;
    private long _totalBetCents; // Store in cents to avoid decimal threading issues
    private long _totalWinCents;

    // Feature tracking
    private long _wildRespinTriggers;
    private long _singleWildTriggers;
    private long _doubleWildTriggers;
    private long _tripleWildTriggers;
    private long _totalRespins;

    // Max win tracking
    private long _maxWinCents;
    private long _maxWinMultiplierTenths; // Store as tenths to avoid decimal issues
    private long _winCapHits; // Times the 500x cap was hit

    // Payline reachability: [paylineId][symbolCode] => (minWin, maxWin, hitCount)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, PaylineSymbolStats>> _paylineStats = new();

    // Multiplier distribution: [symbolCode][matchCount] => count
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, long>> _multiplierDistribution = new();

    // Symbol-level win tracking
    private readonly ConcurrentDictionary<string, long> _symbolWinCounts = new();
    private readonly ConcurrentDictionary<string, long> _symbolWinAmountCents = new();

    // Payline win tracking
    private readonly ConcurrentDictionary<int, long> _paylineHitCounts = new();

    /// <summary>
    /// Records a single spin result.
    /// </summary>
    public void RecordSpin(SpinResult result)
    {
        Interlocked.Increment(ref _totalSpins);
        Interlocked.Add(ref _totalBetCents, (long)(result.BetAmount * 100));
        Interlocked.Add(ref _totalWinCents, (long)(result.WinAmount * 100));

        if (result.WinAmount > 0)
        {
            Interlocked.Increment(ref _winningSpins);
        }

        // Track max win
        var winCents = (long)(result.WinAmount * 100);
        var currentMax = Interlocked.Read(ref _maxWinCents);
        while (winCents > currentMax)
        {
            var original = Interlocked.CompareExchange(ref _maxWinCents, winCents, currentMax);
            if (original == currentMax) break;
            currentMax = original;
        }

        // Track max win multiplier
        var multiplierTenths = (long)(result.WinMultiplier * 10);
        var currentMaxMult = Interlocked.Read(ref _maxWinMultiplierTenths);
        while (multiplierTenths > currentMaxMult)
        {
            var original = Interlocked.CompareExchange(ref _maxWinMultiplierTenths, multiplierTenths, currentMaxMult);
            if (original == currentMaxMult) break;
            currentMaxMult = original;
        }

        // Track win cap hits (500x)
        if (result.WinCapHit)
        {
            Interlocked.Increment(ref _winCapHits);
        }

        // Track wild/respin feature
        if (result.WildReelCount > 0)
        {
            Interlocked.Increment(ref _wildRespinTriggers);
            
            switch (result.WildReelCount)
            {
                case 1:
                    Interlocked.Increment(ref _singleWildTriggers);
                    break;
                case 2:
                    Interlocked.Increment(ref _doubleWildTriggers);
                    break;
                case 3:
                    Interlocked.Increment(ref _tripleWildTriggers);
                    break;
            }
        }

        if (result.IsRespin)
        {
            Interlocked.Increment(ref _totalRespins);
        }

        // Track payline wins
        foreach (var win in result.Wins)
        {
            if (win.PaylineId.HasValue)
            {
                var paylineId = win.PaylineId.Value;
                var symbolCode = win.SymbolCode;
                var winAmountCents = (long)(win.PayoutAmount * 100);

                // Get or create payline stats dictionary
                var symbolStats = _paylineStats.GetOrAdd(paylineId, _ => new ConcurrentDictionary<string, PaylineSymbolStats>());
                var stats = symbolStats.GetOrAdd(symbolCode, _ => new PaylineSymbolStats());
                stats.RecordWin(winAmountCents, win.MatchCount);

                // Track payline hit count
                _paylineHitCounts.AddOrUpdate(paylineId, 1, (_, count) => count + 1);
            }

            // Track multiplier distribution
            var multDist = _multiplierDistribution.GetOrAdd(win.SymbolCode, _ => new ConcurrentDictionary<int, long>());
            multDist.AddOrUpdate(win.MatchCount, 1, (_, count) => count + 1);

            // Track symbol-level wins
            _symbolWinCounts.AddOrUpdate(win.SymbolCode, 1, (_, count) => count + 1);
            _symbolWinAmountCents.AddOrUpdate(win.SymbolCode, (long)(win.PayoutAmount * 100), (_, total) => total + (long)(win.PayoutAmount * 100));
        }
    }

    /// <summary>
    /// Gets a snapshot of the current statistics.
    /// </summary>
    public StatisticsSnapshot GetSnapshot()
    {
        var totalSpins = Interlocked.Read(ref _totalSpins);
        var winningSpins = Interlocked.Read(ref _winningSpins);
        var totalBetCents = Interlocked.Read(ref _totalBetCents);
        var totalWinCents = Interlocked.Read(ref _totalWinCents);

        return new StatisticsSnapshot
        {
            TotalSpins = totalSpins,
            WinningSpins = winningSpins,
            TotalBet = totalBetCents / 100m,
            TotalWin = totalWinCents / 100m,
            RTP = totalBetCents > 0 ? (decimal)totalWinCents / totalBetCents * 100m : 0m,
            HitFrequency = totalSpins > 0 ? (decimal)winningSpins / totalSpins * 100m : 0m,
            
            // Feature stats
            WildRespinTriggers = Interlocked.Read(ref _wildRespinTriggers),
            SingleWildTriggers = Interlocked.Read(ref _singleWildTriggers),
            DoubleWildTriggers = Interlocked.Read(ref _doubleWildTriggers),
            TripleWildTriggers = Interlocked.Read(ref _tripleWildTriggers),
            TotalRespins = Interlocked.Read(ref _totalRespins),
            FeatureFrequency = totalSpins > 0 ? (decimal)Interlocked.Read(ref _wildRespinTriggers) / totalSpins * 100m : 0m,
            
            // Max win stats
            MaxWin = Interlocked.Read(ref _maxWinCents) / 100m,
            MaxWinMultiplier = Interlocked.Read(ref _maxWinMultiplierTenths) / 10m,
            WinCapHits = Interlocked.Read(ref _winCapHits),
            
            // Detailed stats
            PaylineStats = _paylineStats.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToDictionary(
                    inner => inner.Key,
                    inner => inner.Value.GetSnapshot())),
            MultiplierDistribution = _multiplierDistribution.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)),
            SymbolWinCounts = _symbolWinCounts.ToDictionary(kv => kv.Key, kv => kv.Value),
            SymbolWinAmounts = _symbolWinAmountCents.ToDictionary(kv => kv.Key, kv => kv.Value / 100m),
            PaylineHitCounts = _paylineHitCounts.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }
}

/// <summary>
/// Thread-safe stats for a single payline/symbol combination.
/// </summary>
public sealed class PaylineSymbolStats
{
    private long _hitCount;
    private long _minWinCents = long.MaxValue;
    private long _maxWinCents;
    private long _totalWinCents;
    private readonly ConcurrentDictionary<int, long> _matchCountDistribution = new();

    public void RecordWin(long winAmountCents, int matchCount)
    {
        Interlocked.Increment(ref _hitCount);
        Interlocked.Add(ref _totalWinCents, winAmountCents);

        // Update min
        var currentMin = Interlocked.Read(ref _minWinCents);
        while (winAmountCents < currentMin)
        {
            var original = Interlocked.CompareExchange(ref _minWinCents, winAmountCents, currentMin);
            if (original == currentMin) break;
            currentMin = original;
        }

        // Update max
        var currentMax = Interlocked.Read(ref _maxWinCents);
        while (winAmountCents > currentMax)
        {
            var original = Interlocked.CompareExchange(ref _maxWinCents, winAmountCents, currentMax);
            if (original == currentMax) break;
            currentMax = original;
        }

        // Track match count distribution
        _matchCountDistribution.AddOrUpdate(matchCount, 1, (_, count) => count + 1);
    }

    public PaylineSymbolStatsSnapshot GetSnapshot()
    {
        var hitCount = Interlocked.Read(ref _hitCount);
        return new PaylineSymbolStatsSnapshot
        {
            HitCount = hitCount,
            MinWin = hitCount > 0 ? Interlocked.Read(ref _minWinCents) / 100m : 0m,
            MaxWin = Interlocked.Read(ref _maxWinCents) / 100m,
            TotalWin = Interlocked.Read(ref _totalWinCents) / 100m,
            MatchCountDistribution = _matchCountDistribution.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }
}

/// <summary>
/// Immutable snapshot of payline/symbol statistics.
/// </summary>
public sealed class PaylineSymbolStatsSnapshot
{
    public long HitCount { get; init; }
    public decimal MinWin { get; init; }
    public decimal MaxWin { get; init; }
    public decimal TotalWin { get; init; }
    public Dictionary<int, long> MatchCountDistribution { get; init; } = new();
}

/// <summary>
/// Immutable snapshot of all statistics.
/// </summary>
public sealed class StatisticsSnapshot
{
    // Core metrics
    public long TotalSpins { get; init; }
    public long WinningSpins { get; init; }
    public decimal TotalBet { get; init; }
    public decimal TotalWin { get; init; }
    public decimal RTP { get; init; }
    public decimal HitFrequency { get; init; }

    // Feature stats
    public long WildRespinTriggers { get; init; }
    public long SingleWildTriggers { get; init; }
    public long DoubleWildTriggers { get; init; }
    public long TripleWildTriggers { get; init; }
    public long TotalRespins { get; init; }
    public decimal FeatureFrequency { get; init; }

    // Max win stats
    public decimal MaxWin { get; init; }
    public decimal MaxWinMultiplier { get; init; }
    public long WinCapHits { get; init; }

    // Detailed stats
    public Dictionary<int, Dictionary<string, PaylineSymbolStatsSnapshot>> PaylineStats { get; init; } = new();
    public Dictionary<string, Dictionary<int, long>> MultiplierDistribution { get; init; } = new();
    public Dictionary<string, long> SymbolWinCounts { get; init; } = new();
    public Dictionary<string, decimal> SymbolWinAmounts { get; init; } = new();
    public Dictionary<int, long> PaylineHitCounts { get; init; } = new();
}

/// <summary>
/// Result of a single spin for statistics tracking.
/// </summary>
public sealed class SpinResult
{
    public decimal BetAmount { get; init; }
    public decimal WinAmount { get; init; }
    public decimal WinMultiplier { get; init; }
    public bool WinCapHit { get; init; }
    public int WildReelCount { get; init; }
    public bool IsRespin { get; init; }
    public List<WinDetail> Wins { get; init; } = new();
}

/// <summary>
/// Details of a single win within a spin.
/// </summary>
public sealed class WinDetail
{
    public string SymbolCode { get; init; } = string.Empty;
    public int MatchCount { get; init; }
    public decimal PayoutAmount { get; init; }
    public int? PaylineId { get; init; }
}
