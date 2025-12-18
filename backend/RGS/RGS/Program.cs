using System.Globalization;
using System.Text.Json;
using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;
using Microsoft.AspNetCore.Http.Json;
using RGS.Contracts;
using RGS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ITimeService, TimeService>();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new MoneyJsonConverter());
});

var engineBaseUrl = builder.Configuration["Engine:BaseUrl"] ?? "http://localhost:5102";
builder.Services.AddHttpClient<IEngineClient, EngineHttpClient>(client =>
{
    client.BaseAddress = new Uri(engineBaseUrl);
});

var app = builder.Build();
var logger = app.Logger;

app.UseExceptionHandler();

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

app.MapPost("/{operatorId}/{gameId}/start",
        (string operatorId,
            string gameId,
            StartRequest request,
            SessionManager sessions,
            ITimeService timeService,
            HttpContext httpContext) =>
        {
            if (request is null)
            {
                return Results.BadRequest("Request payload is required.");
            }

            var funMode = request.FunMode == 1;
            if (!funMode && string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.BadRequest("token is required when funMode=0.");
            }

            var session = sessions.CreateSession(operatorId, gameId, request.Token ?? string.Empty, funMode);
            var timestamp = timeService.UtcNow;

            // Internal response for tracking
            var internalResponse = new StartResponse(
                SessionId: session.SessionId,
                GameId: gameId,
                OperatorId: operatorId,
                FunMode: request.FunMode,
                CreatedAt: timestamp,
                TimeSignature: timeService.UnixMilliseconds.ToString(CultureInfo.InvariantCulture),
                ThemeId: gameId);

            // Transform to client-compliant response
            // TODO: Get actual player balance, currency, and client info from player service/database
            // For now, using defaults/demo values
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var playerId = request.Token ?? "DEMO_PLAYER";
            var clientResponse = ResponseTransformer.ToClientStartResponse(
                internalResponse: internalResponse,
                playerId: playerId,
                balance: 10000m, // TODO: Get from player service
                clientType: request.Client ?? "desktop",
                clientIp: clientIp,
                countryCode: "US", // TODO: Get from geo-location
                countryName: "United States",
                currencySymbol: "$",
                currencyIsoCode: "USD",
                currencyName: "US Dollar",
                decimalSeparator: ".",
                thousandSeparator: ",",
                currencyDecimals: 2,
                rtp: 96.5m, // TODO: Get from game configuration
                defaultBet: 1m, // TODO: Get from game configuration
                betLevels: new[] { 0.5m, 1m, 2m, 3m, 5m, 10m }, // TODO: Get from game configuration
                maxWinCap: 0m); // TODO: Get from game configuration

            return Results.Ok(clientResponse);
        })
    .WithName("StartGame");

app.MapPost("/{operatorId}/{gameId}/play",
        async (string operatorId,
            string gameId,
            ClientPlayRequest request,
            SessionManager sessions,
            IEngineClient engineClient,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest("Request payload is required.");
            }

            if (!sessions.TryGetSession(request.SessionId, out var session))
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(session.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Session does not match game.");
            }

            if (!TryParseBetMode(request.BetMode, out var betMode))
            {
                return Results.BadRequest("Unknown betMode.");
            }

            if (request.Bets is null || request.Bets.Count == 0)
            {
                return Results.BadRequest("bets array is required.");
            }

            Money baseBet;
            try
            {
                baseBet = new Money(request.BaseBet);
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Invalid baseBet value: {ex.Message}");
            }

            var totalBet = CalculateTotalBet(baseBet, betMode);

            if (totalBet.Amount <= 0)
            {
                return Results.BadRequest("Total bet must be positive.");
            }

            List<BetRequest> betRequests;
            try
            {
                betRequests = ConvertBetRequests(request.Bets);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }

            var engineRequest = new PlayRequest(
                GameId: gameId,
                PlayerToken: session.PlayerToken,
                Bets: betRequests,
                BaseBet: baseBet,
                TotalBet: totalBet,
                BetMode: betMode,
                IsFeatureBuy: false,
                EngineState: session.State ?? EngineSessionState.Create(),
                UserPayload: request.UserPayload,
                LastResponse: request.LastResponse);

            var engineResponse = await engineClient.PlayAsync(engineRequest, cancellationToken);
            sessions.UpdateState(session.SessionId, engineResponse.NextState);

            // TODO: Calculate actual balance changes from player service
            // For now, using demo values
            var prevBalance = 10000m; // TODO: Get from player service
            var balance = prevBalance - totalBet.Amount + engineResponse.Win.Amount; // TODO: Update via player service
            var maxWinCap = 0m; // TODO: Get from game configuration
            var currencyId = "USD"; // TODO: Get from session/player

            // Transform engine response to client-compliant format
            var clientResponse = ResponseTransformer.ToClientPlayResponse(
                engineResponse: engineResponse,
                sessionId: session.SessionId,
                prevBalance: prevBalance,
                balance: balance,
                bet: totalBet.Amount,
                currencyId: currencyId,
                maxWinCap: maxWinCap);

            return Results.Ok(clientResponse);
        })
    .WithName("Play");

app.MapPost("/{gameId}/buy-free-spins",
        async (string gameId,
            BuyFeatureRequest request,
            SessionManager sessions,
            IEngineClient engineClient,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest("Request payload is required.");
            }

            if (!sessions.TryGetSession(request.SessionId, out var session))
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(session.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Session does not match game.");
            }

            if (!TryParseBetMode(request.BetMode, out var betMode))
            {
                return Results.BadRequest("Unknown betMode.");
            }

            if (betMode != BetMode.Standard)
            {
                return Results.BadRequest("ANTE_MODE_BUY_NOT_ALLOWED");
            }

            Money baseBet;
            try
            {
                baseBet = new Money(request.BaseBet);
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Invalid baseBet value: {ex.Message}");
            }

            var totalBet = CalculateTotalBet(baseBet, betMode);
            if (totalBet.Amount <= 0)
            {
                return Results.BadRequest("Total bet must be positive.");
            }

            List<BetRequest> betRequests;
            try
            {
                betRequests = request.Bets is { Count: > 0 }
                    ? ConvertBetRequests(request.Bets)
                    : new List<BetRequest> { new("BASE", baseBet) };
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }

            var engineRequest = new PlayRequest(
                GameId: gameId,
                PlayerToken: session.PlayerToken,
                Bets: betRequests,
                BaseBet: baseBet,
                TotalBet: totalBet,
                BetMode: betMode,
                IsFeatureBuy: true,
                EngineState: session.State ?? EngineSessionState.Create(),
                UserPayload: request.UserPayload,
                LastResponse: null);

            var engineResponse = await engineClient.PlayAsync(engineRequest, cancellationToken);
            sessions.UpdateState(session.SessionId, engineResponse.NextState);
            if (engineResponse.BuyCost.Amount > 0)
            {
                logger.LogInformation("Buy feature charged {Amount}", engineResponse.BuyCost.Amount);
            }
            return Results.Ok(engineResponse);
        })
    .WithName("BuyFreeSpins");

app.MapPost("/{operatorId}/player/balance",
        (string operatorId,
            ClientBalanceRequest request,
            SessionManager sessions) =>
        {
            if (request is null)
            {
                return Results.BadRequest("Request payload is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return Results.BadRequest("playerId is required.");
            }

            // TODO: Get actual balance from player service/database
            // For now, try to find session and return demo balance
            var balance = 10000m; // TODO: Get from player service using request.PlayerId
            
            var response = new ClientBalanceResponse(
                StatusCode: 8000,
                Message: "Request processed successfully",
                Balance: balance);

            return Results.Ok(response);
        })
    .WithName("GetBalance");

app.Run();

static bool TryParseBetMode(string? value, out BetMode mode)
{
    if (string.Equals(value, "ante", StringComparison.OrdinalIgnoreCase))
    {
        mode = BetMode.Ante;
        return true;
    }

    if (string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
    {
        mode = BetMode.Standard;
        return true;
    }

    mode = BetMode.Standard;
    return false;
}

static Money CalculateTotalBet(Money baseBet, BetMode mode)
{
    var multiplier = mode == BetMode.Ante ? 1.25m : 1m;
    return new Money(baseBet.Amount * multiplier);
}

static List<BetRequest> ConvertBetRequests(IReadOnlyList<ClientBetRequest> bets)
{
    var betRequests = new List<BetRequest>(bets.Count);
    foreach (var bet in bets)
    {
        try
        {
            betRequests.Add(new BetRequest(bet.BetType, new Money(bet.Amount)));
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid bet entry: {ex.Message}");
        }
    }

    return betRequests;
}
