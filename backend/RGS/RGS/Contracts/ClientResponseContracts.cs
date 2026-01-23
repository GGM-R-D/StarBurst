namespace RGS.Contracts;

// Client-RGS compliant response structures

public sealed record ClientStartResponse(
    int StatusCode,
    string Message,
    ClientPlayer Player,
    ClientInfo Client,
    ClientCurrency Currency,
    ClientGame Game);

public sealed record ClientPlayer(
    string SessionId,
    string Id,
    decimal Balance);

public sealed record ClientInfo(
    string Type,
    string Ip,
    ClientCountry Country);

public sealed record ClientCountry(
    string Code,
    string Name);

public sealed record ClientCurrency(
    string Symbol,
    string IsoCode,
    string Name,
    ClientSeparator Separator,
    int Decimals);

public sealed record ClientSeparator(
    string Decimal,
    string Thousand);

public sealed record ClientGame(
    decimal Rtp,
    int Mode,
    ClientBet Bet,
    bool FunMode,
    decimal MaxWinCap,
    ClientGameConfig Config,
    ClientFreeSpins FreeSpins,
    ClientPromoFreeSpins PromoFreeSpins,
    ClientFeature Feature,
    ClientLastPlay LastPlay);

public sealed record ClientBet(
    int Default,  // Index into Levels array (per spec)
    IReadOnlyList<decimal> Levels);

public sealed record ClientGameConfig(
    string? StartScreen,
    ClientSettings Settings);

public sealed record ClientSettings(
    string IsAutoplay,
    string IsSlamStop,
    string IsBuyFeatures,
    string IsTurboSpin,
    string IsRealityCheck,
    string MinSpin,
    string MaxSpin);

public sealed record ClientFreeSpins(
    int Amount,
    int Left,
    decimal BetValue,
    decimal RoundWin,
    decimal TotalWin,
    decimal TotalBet);

public sealed record ClientPromoFreeSpins(
    int Amount,
    int Left,
    decimal BetValue,
    bool IsPromotion,
    decimal TotalWin,
    decimal TotalBet);

public sealed record ClientFeature(
    string Name,
    string Type);

public sealed record ClientLastPlay(
    ClientBetLevel BetLevel,
    IReadOnlyList<object> Results);

public sealed record ClientBetLevel(
    int Index,
    decimal Value);

public sealed record ClientPlayResponse(
    int StatusCode,
    string Message,
    ClientPlayPlayer Player,
    ClientPlayGame Game,
    ClientPlayFreeSpins FreeSpins,
    ClientPlayPromoFreeSpins PromoFreeSpins,
    IReadOnlyList<object> Jackpots,
    ClientPlayFeature Feature);

public sealed record ClientPlayPlayer(
    string SessionId,
    string RoundId,
    ClientTransaction Transaction,
    decimal PrevBalance,
    decimal Balance,
    decimal Bet,
    decimal Win,
    string CurrencyId);

public sealed record ClientTransaction(
    string Withdraw,
    string Deposit);

public sealed record ClientPlayGame(
    object Results, // Game engine results
    int Mode,
    ClientMaxWinCap MaxWinCap);

public sealed record ClientMaxWinCap(
    bool Achieved,
    decimal Value,
    string RealWin);

public sealed record ClientPlayFreeSpins(
    int Amount,
    int Left,
    bool IsPromotion,
    decimal BetValue,  // Added per spec - bet value used with free spin
    decimal RoundWin,
    decimal TotalWin,
    decimal TotalBet,
    decimal Won);

public sealed record ClientPlayPromoFreeSpins(
    int Amount,
    int Left,
    decimal BetValue,
    int Level,
    decimal TotalWin,
    decimal TotalBet);

/// <summary>
/// Information about an expanding wild on a specific reel for client response.
/// </summary>
public sealed record ClientExpandingWildInfo(
    int Reel, // Reel index (0-based: 1,2,3 = reels 2,3,4)
    IReadOnlyList<int> Rows); // Row indices (0-based: 0,1,2 = top, middle, bottom)

/// <summary>
/// Feature information in client response.
/// Extended for Starburst expanding wilds feature.
/// </summary>
public sealed record ClientPlayFeature(
    string Name,
    string Type,
    int IsClosure,
    // Extended fields for expanding wilds feature
    bool? Active = null,
    int? RespinsAwarded = null,
    int? RespinsRemaining = null,
    IReadOnlyList<int>? LockedReels = null,
    IReadOnlyList<ClientExpandingWildInfo>? ExpandingWilds = null,
    IReadOnlyList<int>? InitialGrid = null); // Initial grid state before wild expansion (for frontend animation)

public sealed record ClientBalanceResponse(
    int StatusCode,
    string Message,
    decimal Balance);

public sealed record ClientBalanceRequest(
    string PlayerId);
