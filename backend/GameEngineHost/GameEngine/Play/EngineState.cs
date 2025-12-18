using GameEngine.Configuration;

namespace GameEngine.Play;

public enum SpinMode
{
    BaseGame,
    FreeSpins,
    BuyEntry
}

public sealed class EngineSessionState
{
    public FreeSpinState? FreeSpins { get; set; }

    public bool IsInFreeSpins => FreeSpins is { SpinsRemaining: > 0 };

    public EngineSessionState Clone() =>
        new()
        {
            FreeSpins = FreeSpins?.Clone()
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

