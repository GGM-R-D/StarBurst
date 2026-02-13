using GameEngine.Configuration;

namespace GameEngine.Play;

public enum SpinMode
{
    BaseGame,
    FreeSpins,
    BuyEntry,
    Respin
}

public sealed class EngineSessionState
{
    public FreeSpinState? FreeSpins { get; set; }
    public RespinState? Respins { get; set; }

    public bool IsInFreeSpins => FreeSpins is { SpinsRemaining: > 0 };
    public bool IsInRespinFeature => Respins is { RespinsRemaining: > 0 };

    public EngineSessionState Clone() =>
        new()
        {
            FreeSpins = FreeSpins?.Clone(),
            Respins = Respins?.Clone()
        };

    public static EngineSessionState Create() => new();
}

public sealed class FreeSpinState
{
    public int SpinsRemaining { get; set; }
    public int TotalSpinsAwarded { get; set; }
    public decimal TotalMultiplier { get; set; }
    public Money FeatureWin { get; set; } = Money.Zero;
    public bool JustTriggered { get; set; }

    public FreeSpinState Clone() => new()
    {
        SpinsRemaining = SpinsRemaining,
        TotalSpinsAwarded = TotalSpinsAwarded,
        TotalMultiplier = TotalMultiplier,
        FeatureWin = FeatureWin,
        JustTriggered = JustTriggered
    };
}

/// <summary>
/// Tracks Starburst Wild Respin feature state.
/// Wilds on reels 2, 3, 4 expand and lock. One respin is awarded per spin when any expanding wilds appear.
/// </summary>
public sealed class RespinState
{
    /// <summary>
    /// Number of respins remaining. One respin is awarded per spin (regardless of how many wilds), not N respins for N wilds.
    /// </summary>
    public int RespinsRemaining { get; set; }
    
    /// <summary>
    /// Locked wild reels (0-based indices: 1, 2, 3 = reels 2, 3, 4).
    /// These reels are expanded with wilds and remain locked during respins.
    /// </summary>
    public HashSet<int> LockedWildReels { get; set; } = new HashSet<int>();
    
    /// <summary>
    /// Total respins awarded so far (for tracking).
    /// </summary>
    public int TotalRespinsAwarded { get; set; }
    
    /// <summary>
    /// Whether the respin feature was just triggered this spin.
    /// </summary>
    public bool JustTriggered { get; set; }

    /// <summary>
    /// Cached final grid (symbol IDs) from the spin that triggered respins. Returned unchanged on follow-up requests while respins decrement.
    /// </summary>
    public IReadOnlyList<int>? CachedGridIds { get; set; }
    
    /// <summary>
    /// Cached total win from the spin that triggered respins.
    /// </summary>
    public Money? CachedTotalWin { get; set; }
    
    /// <summary>
    /// Cached symbol wins from the spin that triggered respins.
    /// </summary>
    public IReadOnlyList<SymbolWin>? CachedWins { get; set; }

    public RespinState Clone() => new()
    {
        RespinsRemaining = RespinsRemaining,
        LockedWildReels = new HashSet<int>(LockedWildReels),
        TotalRespinsAwarded = TotalRespinsAwarded,
        JustTriggered = JustTriggered,
        CachedGridIds = CachedGridIds,
        CachedTotalWin = CachedTotalWin,
        CachedWins = CachedWins
    };
}

