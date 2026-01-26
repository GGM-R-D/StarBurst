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
    JsonElement? Currency = null, // Currency object with id property (e.g., {"id": "EUR"})
    bool FunMode = false); // If true, uses pre-configured fun mode grids instead of RNG

/// <summary>
/// Bet request from RGS. Per platform spec, only 'amount' is guaranteed.
/// BetType is optional and may not be provided by all RGS implementations.
/// </summary>
public sealed record BetRequest(Money Amount, string? BetType = null);

/// <summary>
/// Play response per RGS-Game server specification.
/// Required fields: statusCode, message, win, freeSpins, results, feature
/// </summary>
public sealed record PlayResponse(
    int StatusCode,           // 200 on success, other codes for errors
    Money Win,                // Total win amount for this play/step
    Money ScatterWin,         // Win from scatter symbols
    Money FeatureWin,         // Win from feature (expanding wilds)
    Money BuyCost,            // Cost if this was a feature buy
    int FreeSpins,            // Number of free spins won (0 if none) - RENAMED from FreeSpinsAwarded per spec
    string RoundId,           // Unique round identifier
    DateTimeOffset Timestamp, // Server timestamp
    EngineSessionState NextState, // Engine state for next spin
    ResultsEnvelope Results,  // Engine-defined results object (forwarded to client as-is)
    string Message,           // String message describing the status code
    FeatureOutcome? Feature = null); // Feature object (type, isClosure mandatory per spec)

/// <summary>
/// Results envelope containing all game outcome data.
/// This object is forwarded to the frontend as-is by the RGS platform.
/// </summary>
public sealed record ResultsEnvelope(
    IReadOnlyList<CascadeStep> Cascades,
    IReadOnlyList<SymbolWin> Wins,
    ScatterOutcome? Scatter,
    FeatureSummary? FreeSpins,
    string RngTransactionId,
    IReadOnlyList<int>? FinalGridSymbols, // Symbol matrix: numeric IDs (0-based index in symbol catalog)
    // NEW: Required by frontend per RGS spec
    IReadOnlyList<int>? Stops = null,     // Reel stop positions [5] - one per reel
    Money? TotalWin = null,               // Total win (duplicate of top-level for frontend convenience)
    ResultsFeature? Feature = null);      // Feature state for safe forwarding to frontend

public sealed record CascadeStep(
    int Index,
    IReadOnlyList<int> GridBefore, // Symbol matrix: numeric IDs
    IReadOnlyList<int> GridAfter, // Symbol matrix: numeric IDs
    IReadOnlyList<SymbolWin> WinsAfterCascade,
    Money BaseWin,
    decimal AppliedMultiplier,
    Money TotalWin);

/// <summary>
/// Represents a single symbol win with all details needed for frontend animation.
/// </summary>
public sealed record SymbolWin(
    string SymbolCode,    // Symbol code (e.g., "BAR", "SEVEN", "RED")
    int Count,            // Number of matching symbols
    decimal Multiplier,   // Payout multiplier
    Money Payout,         // Win amount
    IReadOnlyList<int>? Indices = null,   // Flat grid indices of winning symbols
    int? PaylineId = null,                // Payline ID (1-10) that this win occurred on
    IReadOnlyList<WinCoordinate>? Coordinates = null); // X/Y coordinates for frontend animation

/// <summary>
/// Coordinate for a winning symbol position.
/// X = column (reel), Y = row
/// </summary>
public sealed record WinCoordinate(int X, int Y);

public sealed record ScatterOutcome(int SymbolCount, Money Win, int FreeSpinsAwarded);

public sealed record FeatureSummary(int SpinsRemaining, decimal TotalMultiplier, Money FeatureWin, bool TriggeredThisSpin);

public enum BetMode
{
    Standard,
    Ante
}

/// <summary>
/// Information about an expanding wild on a specific reel.
/// </summary>
public sealed record ExpandingWildInfo(
    int Reel, // Reel index (0-based: 1,2,3 = reels 2,3,4)
    IReadOnlyList<int> Rows); // Row indices (0-based: 0,1,2 = top, middle, bottom) where wilds appear before expansion

/// <summary>
/// Feature outcome returned when a game feature is triggered.
/// Per RGS-Game server specification: type and isClosure are mandatory, name is optional.
/// Extended for Starburst expanding wilds feature.
/// </summary>
public sealed record FeatureOutcome(
    string Type, // Feature type (e.g., "BONUS_GAME", "GAMBLE_GAME", "FREESPINS", "EXPANDING_WILDS")
    int IsClosure, // 1 if last round of feature, 0 otherwise
    string? Name = null, // Optional feature name
    // Extended fields for expanding wilds feature
    bool? Active = null, // Whether the feature is currently active
    int? RespinsAwarded = null, // Total respins awarded
    int? RespinsRemaining = null, // Respins remaining
    IReadOnlyList<int>? LockedReels = null, // Locked reel indices (0-based: 1,2,3 = reels 2,3,4)
    IReadOnlyList<ExpandingWildInfo>? ExpandingWilds = null, // Expanding wilds with row information
    IReadOnlyList<int>? InitialGrid = null); // Initial grid state before wild expansion (for frontend animation)

/// <summary>
/// Feature state inside results for safe forwarding to frontend.
/// The RGS platform forwards results as-is, so feature state here is guaranteed to reach the client.
/// This is the recommended place for frontend to read feature state.
/// </summary>
public sealed record ResultsFeature(
    string Type,              // Feature type (e.g., "EXPANDING_WILDS", "none")
    bool Active,              // Whether the feature is currently active
    int RespinsAwarded,       // Total respins awarded
    int RespinsRemaining,     // Respins remaining
    int IsClosure,            // 1 if last round of feature, 0 otherwise (duplicated from top-level for safe forwarding)
    IReadOnlyList<int>? LockedReels = null,           // Locked reel indices (0-based)
    IReadOnlyList<ExpandingWildInfo>? ExpandingWilds = null,  // Expanding wilds info
    IReadOnlyList<int>? InitialGridSymbols = null);   // Grid state before wild expansion

