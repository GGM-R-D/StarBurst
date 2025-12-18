using System.Collections.Generic;
using System.Text.Json;
using GameEngine.Configuration;

namespace GameEngine.Play;

public sealed record PlayRequest(
    string GameId,
    string PlayerToken,
    IReadOnlyList<BetRequest> Bets,
    Money BaseBet,
    Money TotalBet,
    BetMode BetMode,
    bool IsFeatureBuy,
    EngineSessionState? EngineState,
    JsonElement? UserPayload,
    JsonElement? LastResponse,
    // Additional fields per RGS-Game server specification
    Money? Bet = null, // Calculated total bet (sum of all amounts in bets array) - IMPORTANT!
    int? RtpLevel = null, // RTP level if game supports multiple RTP (1,2,3,4 etc.)
    int? Mode = null, // Game mode: 0=normal, 1=free spin, 2=bonus game, 3=free bets
    JsonElement? Currency = null); // Currency object with id property (e.g., {"id": "EUR"})

public sealed record BetRequest(string BetType, Money Amount);

public sealed record PlayResponse(
    int StatusCode,
    Money Win,
    Money ScatterWin,
    Money FeatureWin,
    Money BuyCost,
    int FreeSpinsAwarded,
    string RoundId,
    DateTimeOffset Timestamp,
    EngineSessionState NextState,
    ResultsEnvelope Results,
    // Additional fields per RGS-Game server specification
    string Message, // String message describing the status code
    FeatureOutcome? Feature = null); // Feature object when feature is triggered (type, isClosure, name)

public sealed record ResultsEnvelope(
    IReadOnlyList<CascadeStep> Cascades,
    IReadOnlyList<SymbolWin> Wins,
    ScatterOutcome? Scatter,
    FeatureSummary? FreeSpins,
    string RngTransactionId,
    IReadOnlyList<int>? FinalGridSymbols); // Symbol matrix: numeric IDs (0-based index in symbol catalog)

public sealed record CascadeStep(
    int Index,
    IReadOnlyList<int> GridBefore, // Symbol matrix: numeric IDs
    IReadOnlyList<int> GridAfter, // Symbol matrix: numeric IDs
    IReadOnlyList<SymbolWin> WinsAfterCascade,
    Money BaseWin,
    decimal AppliedMultiplier,
    Money TotalWin);

public sealed record SymbolWin(
    string SymbolCode,
    int Count,
    decimal Multiplier,
    Money Payout,
    IReadOnlyList<int>? Indices = null);

public sealed record ScatterOutcome(int SymbolCount, Money Win, int FreeSpinsAwarded);

public sealed record FeatureSummary(int SpinsRemaining, decimal TotalMultiplier, Money FeatureWin, bool TriggeredThisSpin);

public enum BetMode
{
    Standard,
    Ante
}

/// <summary>
/// Feature outcome returned when a game feature is triggered.
/// Per RGS-Game server specification: type and isClosure are mandatory, name is optional.
/// </summary>
public sealed record FeatureOutcome(
    string Type, // Feature type (e.g., "BONUS_GAME", "GAMBLE_GAME", "FREESPINS")
    int IsClosure, // 1 if last round of feature, 0 otherwise
    string? Name = null); // Optional feature name

