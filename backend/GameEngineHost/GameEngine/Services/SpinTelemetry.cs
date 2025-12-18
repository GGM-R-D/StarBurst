using GameEngine.Configuration;
using GameEngine.Play;
using Microsoft.Extensions.Logging;

namespace GameEngine.Services;

public interface ISpinTelemetrySink
{
    void Record(SpinTelemetryEvent telemetryEvent);
}

public sealed class NullSpinTelemetrySink : ISpinTelemetrySink
{
    public void Record(SpinTelemetryEvent telemetryEvent)
    {
        // no-op
    }
}

public sealed class LoggingSpinTelemetrySink : ISpinTelemetrySink
{
    private readonly ILogger<LoggingSpinTelemetrySink> _logger;

    public LoggingSpinTelemetrySink(ILogger<LoggingSpinTelemetrySink> logger)
    {
        _logger = logger;
    }

    public void Record(SpinTelemetryEvent telemetryEvent)
    {
        _logger.LogInformation(
            "Spin {@GameId} mode={BetMode} spinMode={SpinMode} wager={Wager} win={Win} scatter={Scatter} feature={Feature} buyCost={BuyCost} cascades={Cascades} fsMultiplier={FsMultiplier}",
            telemetryEvent.GameId,
            telemetryEvent.BetMode,
            telemetryEvent.SpinMode,
            telemetryEvent.TotalBet,
            telemetryEvent.TotalWin,
            telemetryEvent.ScatterWin,
            telemetryEvent.FeatureWin,
            telemetryEvent.BuyCost,
            telemetryEvent.Cascades,
            telemetryEvent.FreeSpinMultiplier);
    }
}

public sealed record SpinTelemetryEvent(
    string GameId,
    BetMode BetMode,
    SpinMode SpinMode,
    decimal TotalBet,
    decimal TotalWin,
    decimal ScatterWin,
    decimal FeatureWin,
    decimal BuyCost,
    int Cascades,
    bool TriggeredFreeSpins,
    decimal FreeSpinMultiplier,
    DateTimeOffset Timestamp);

