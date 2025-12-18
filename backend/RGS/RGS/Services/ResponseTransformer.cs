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
        var maxWinAchieved = engineResponse.Win.Amount >= maxWinCap && maxWinCap > 0;

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
                Results: engineResponse.Results, // Pass through engine results
                Mode: 0,
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
                Name: freeSpins is not null && freeSpins.SpinsRemaining > 0 ? "FREESPINS" : "",
                Type: freeSpins is not null && freeSpins.SpinsRemaining > 0 ? "FREESPINS" : "",
                IsClosure: freeSpins is not null && freeSpins.SpinsRemaining == 0 ? 1 : 0));
    }
}

