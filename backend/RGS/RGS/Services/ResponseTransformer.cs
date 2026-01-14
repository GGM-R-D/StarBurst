using System.Linq;
using GameEngine.Play;
using RGS.Contracts;

namespace RGS.Services;

public static class ResponseTransformer
{
    public static ClientStartResponse ToClientStartResponse(
        StartResponse internalResponse,
        string playerId,
        decimal balance,
        string clientType,
        string clientIp,
        string countryCode,
        string countryName,
        string currencySymbol,
        string currencyIsoCode,
        string currencyName,
        string decimalSeparator,
        string thousandSeparator,
        int currencyDecimals,
        decimal rtp,
        decimal defaultBet,
        IReadOnlyList<decimal> betLevels,
        decimal maxWinCap)
    {
        return new ClientStartResponse(
            StatusCode: 6000,
            Message: "Request processed successfully",
            Player: new ClientPlayer(
                SessionId: internalResponse.SessionId,
                Id: playerId,
                Balance: balance),
            Client: new ClientInfo(
                Type: clientType,
                Ip: clientIp,
                Country: new ClientCountry(Code: countryCode, Name: countryName)),
            Currency: new ClientCurrency(
                Symbol: currencySymbol,
                IsoCode: currencyIsoCode,
                Name: currencyName,
                Separator: new ClientSeparator(Decimal: decimalSeparator, Thousand: thousandSeparator),
                Decimals: currencyDecimals),
            Game: new ClientGame(
                Rtp: rtp,
                Mode: 0,
                Bet: new ClientBet(Default: defaultBet, Levels: betLevels),
                FunMode: internalResponse.FunMode == 1,
                MaxWinCap: maxWinCap,
                Config: new ClientGameConfig(
                    StartScreen: null,
                    Settings: new ClientSettings(
                        IsAutoplay: "1",
                        IsSlamStop: "1",
                        IsBuyFeatures: "1",
                        IsTurboSpin: "1",
                        IsRealityCheck: "1",
                        MinSpin: "0",
                        MaxSpin: "0")),
                FreeSpins: new ClientFreeSpins(0, 0, 0, 0, 0, 0),
                PromoFreeSpins: new ClientPromoFreeSpins(0, 0, 0, false, 0, 0),
                Feature: new ClientFeature("", ""),
                LastPlay: new ClientLastPlay(
                    BetLevel: new ClientBetLevel(0, 0),
                    Results: Array.Empty<object>())));
    }

    public static ClientPlayResponse ToClientPlayResponse(
        PlayResponse engineResponse,
        string sessionId,
        decimal prevBalance,
        decimal balance,
        decimal bet,
        string currencyId,
        decimal maxWinCap)
    {
        var freeSpins = engineResponse.NextState.FreeSpins;
        var respins = engineResponse.NextState.Respins; // Get respin state from backend
        var maxWinAchieved = engineResponse.Win.Amount >= maxWinCap && maxWinCap > 0;
        
        // Determine feature info - check respins first (Starburst uses respins, not free spins)
        var featureName = "";
        var featureType = "";
        var featureIsClosure = 0;
        
        if (respins is not null && respins.RespinsRemaining > 0)
        {
            featureName = "Starburst Wilds";
            featureType = "BONUS_GAME";
            featureIsClosure = 0;
            
            // Log wild feature
            var lockedReelsStr = respins.LockedWildReels != null && respins.LockedWildReels.Count > 0
                ? string.Join(",", respins.LockedWildReels.Select(r => r + 1))
                : "none";
            GameLogger.LogWildFeature(
                engineResponse.RoundId,
                respins.LockedWildReels?.FirstOrDefault() ?? -1,
                1, // Respins awarded (1 per wild reel)
                respins.RespinsRemaining,
                lockedReelsStr);
        }
        else if (respins is not null && respins.RespinsRemaining == 0)
        {
            // Feature just ended
            featureName = "Starburst Wilds";
            featureType = "BONUS_GAME";
            featureIsClosure = 1;
        }
        else if (freeSpins is not null && freeSpins.SpinsRemaining > 0)
        {
            featureName = "FREESPINS";
            featureType = "FREESPINS";
            featureIsClosure = 0;
        }
        else if (freeSpins is not null && freeSpins.SpinsRemaining == 0)
        {
            featureName = "FREESPINS";
            featureType = "FREESPINS";
            featureIsClosure = 1;
        }

        return new ClientPlayResponse(
            StatusCode: 6000,
            Message: "Request processed successfully",
            Player: new ClientPlayPlayer(
                SessionId: sessionId,
                RoundId: engineResponse.RoundId,
                Transaction: new ClientTransaction(
                    Withdraw: Guid.NewGuid().ToString("N"),
                    Deposit: Guid.NewGuid().ToString("N")),
                PrevBalance: prevBalance,
                Balance: balance,
                Bet: bet,
                Win: engineResponse.Win.Amount,
                CurrencyId: currencyId),
            Game: new ClientPlayGame(
                Results: engineResponse.Results, // Pass through engine results (includes nextState in Results if needed)
                Mode: respins is not null && respins.RespinsRemaining > 0 ? 2 : 0, // Mode 2 = bonus game (respin feature)
                MaxWinCap: new ClientMaxWinCap(
                    Achieved: maxWinAchieved,
                    Value: maxWinCap,
                    RealWin: maxWinAchieved ? maxWinCap.ToString("F2") : engineResponse.Win.Amount.ToString("F2"))),
            FreeSpins: new ClientPlayFreeSpins(
                Amount: freeSpins?.TotalSpinsAwarded ?? 0,
                Left: freeSpins?.SpinsRemaining ?? 0,
                IsPromotion: false,
                RoundWin: engineResponse.FeatureWin.Amount,
                TotalWin: freeSpins?.FeatureWin.Amount ?? 0,
                TotalBet: 0,
                Won: engineResponse.FreeSpinsAwarded > 0 ? engineResponse.Win.Amount : 0),
            PromoFreeSpins: new ClientPlayPromoFreeSpins(0, 0, 0, 0, 0, 0),
            Jackpots: Array.Empty<object>(),
            Feature: new ClientPlayFeature(
                Name: featureName,
                Type: featureType,
                IsClosure: featureIsClosure));
    }
}

