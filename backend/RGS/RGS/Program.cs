using System.Globalization;
using System.Text.Json;
using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;
using Microsoft.AspNetCore.Http.Json;
using RGS.Contracts;
using RGS.Services;
using RGS;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// TEMPORARY CORS: allow any origin to call the RGS (browser-friendly, no credentials)
// NOTE: Do NOT use AllowCredentials with AllowAnyOrigin.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
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

// Enable CORS early so OPTIONS and API calls succeed
app.UseCors("AllowAll");

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/{operatorId}/{gameId}/start",
        (string operatorId,
            string gameId,
            StartRequest request,
            SessionManager sessions,
            ITimeService timeService,
            HttpContext httpContext,
            IConfiguration configuration) =>
        {
            if (request is null)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ErrorCodes.GetMessage(ErrorCodes.BadRequest) }, statusCode: 200);
            }

            // Check for forced fun mode override (for testing/demo purposes)
            // Set via appsettings.json: "ForceFunMode": true
            // Or environment variable: FORCE_FUN_MODE=true
            var forceFunMode = configuration.GetValue<bool>("ForceFunMode", false) || 
                              Environment.GetEnvironmentVariable("FORCE_FUN_MODE") == "true";
            
            // Determine effective fun mode
            var effectiveFunMode = forceFunMode ? 1 : request.FunMode;
            var funMode = effectiveFunMode == 1;
            
            if (forceFunMode)
            {
                Console.WriteLine($"[RGS] ForceFunMode enabled - overriding request funMode={request.FunMode} to funMode=1 (demo mode)");
            }
            
            if (!funMode && string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "token is required when funMode=0." }, statusCode: 200);
            }
            
            // Handle currencyId if provided (optional, only used if funMode=1 and no default currency)
            // For now, we'll use the default currency from configuration
            // TODO: Implement currency selection logic based on currencyId parameter

            // Create session with initial balance (default 10000, or from player service in production)
            var initialBalance = 10000m; // TODO: Get from player service using request.Token
            var session = sessions.CreateSession(operatorId, gameId, request.Token ?? string.Empty, funMode, initialBalance);
            var timestamp = timeService.UtcNow;

            // Log session start
            var playerId = request.Token ?? "DEMO_PLAYER";
            GameLogger.LogSessionStart(session.SessionId, gameId, initialBalance, playerId);

            // Internal response for tracking (use effective fun mode, not original request)
            var internalResponse = new StartResponse(
                SessionId: session.SessionId,
                GameId: gameId,
                OperatorId: operatorId,
                FunMode: effectiveFunMode, // Use effective fun mode (may be overridden)
                CreatedAt: timestamp,
                TimeSignature: timeService.UnixMilliseconds.ToString(CultureInfo.InvariantCulture),
                ThemeId: gameId);

            // Transform to client-compliant response
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var betLevels = new[] { 0.5m, 1m, 2m, 3m, 5m, 10m }; // TODO: Get from game configuration
            var defaultBetIndex = 1; // Index into betLevels (1 = 1m, the second item)
            var clientResponse = ResponseTransformer.ToClientStartResponse(
                internalResponse: internalResponse,
                playerId: playerId,
                balance: session.Balance, // Use session balance
                clientType: request.Client ?? "desktop",
                clientIp: clientIp,
                countryCode: "RSA", // TODO: Get from geo-location
                countryName: "South Africa",
                currencySymbol: "R",
                currencyIsoCode: "ZAR",
                currencyName: "South African Rand",
                decimalSeparator: ".",
                thousandSeparator: ",",
                currencyDecimals: 2,
                rtp: 96.5m, // TODO: Get from game configuration
                defaultBetIndex: defaultBetIndex, // Index into betLevels array (per spec)
                betLevels: betLevels,
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
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ErrorCodes.GetMessage(ErrorCodes.BadRequest) }, statusCode: 200);
            }

            if (!sessions.TryGetSession(request.SessionId, out var session))
            {
                return Results.Json(new { statusCode = ErrorCodes.Unauthorized, message = ErrorCodes.GetMessage(ErrorCodes.Unauthorized) }, statusCode: 200);
            }

            if (!string.Equals(session.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Session does not match game." }, statusCode: 200);
            }

            if (!TryParseBetMode(request.BetMode, out var betMode))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Unknown betMode." }, statusCode: 200);
            }

            if (request.Bets is null || request.Bets.Count == 0)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "bets array is required." }, statusCode: 200);
            }

            Money baseBet;
            try
            {
                baseBet = new Money(request.BaseBet);
            }
            catch (Exception ex)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = $"Invalid baseBet value: {ex.Message}" }, statusCode: 200);
            }

            var totalBet = CalculateTotalBet(baseBet, betMode);

            if (totalBet.Amount <= 0)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Total bet must be positive." }, statusCode: 200);
            }

            List<BetRequest> betRequests;
            try
            {
                betRequests = ConvertBetRequests(request.Bets);
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ex.Message }, statusCode: 200);
            }

            // Log fun mode status for debugging
            Console.WriteLine($"[RGS] Play request - Session FunMode: {session.FunMode}, SessionId: {session.SessionId}");
            
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
                LastResponse: request.LastResponse,
                FunMode: session.FunMode); // Pass funMode from session to engine
            
            Console.WriteLine($"[RGS] Engine request created - FunMode: {engineRequest.FunMode}");

            // Log play request
            var isRespinBeforeSpin = session.State?.Respins is not null && session.State.Respins.RespinsRemaining > 0;
            var lockedReelsInfo = session.State?.Respins?.LockedWildReels != null && session.State.Respins.LockedWildReels.Count > 0
                ? string.Join(",", session.State.Respins.LockedWildReels.Select(r => r + 1))
                : "none";
            GameLogger.LogPlayRequest(
                session.SessionId,
                "pending", // RoundId not yet known
                totalBet.Amount,
                betMode.ToString(),
                isRespinBeforeSpin,
                session.State?.Respins?.RespinsRemaining);
            
            // Log state details for debugging
            if (isRespinBeforeSpin && session.State?.Respins != null)
            {
                GameLogger.LogInfo($"RESPIN REQUEST - State details: RespinsRemaining={session.State.Respins.RespinsRemaining}, LockedWildReels (0-based)=[{string.Join(", ", session.State.Respins.LockedWildReels)}], LockedWildReels (1-based)=[{lockedReelsInfo}]");
            }

            PlayResponse engineResponse;
            try
            {
                engineResponse = await engineClient.PlayAsync(engineRequest, cancellationToken);
            }
            catch (RGS.Services.EngineCallException ex)
            {
                // Don't crash the RGS process on engine validation failures.
                // Per RGS style in this project, return HTTP 200 with an internal statusCode.
                var mappedStatus = ex.HttpStatusCode == 400 ? ErrorCodes.BadRequest : ErrorCodes.InternalServerError;
                var message = !string.IsNullOrWhiteSpace(ex.ErrorContent)
                    ? ex.ErrorContent
                    : ErrorCodes.GetMessage(mappedStatus);

                logger.LogError(ex, "Engine call failed: {Message}", message);
                return Results.Json(new { statusCode = mappedStatus, message }, statusCode: 200);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while calling engine");
                return Results.Json(new { statusCode = ErrorCodes.InternalServerError, message = ErrorCodes.GetMessage(ErrorCodes.InternalServerError) }, statusCode: 200);
            }
            sessions.UpdateState(session.SessionId, engineResponse.NextState);

            // Get current balance from session (before update)
            var prevBalance = session.Balance;
            
            // Check if this is a respin - respins should be FREE (no bet deduction)
            // A respin is ONLY when the previous state had respins remaining (before the spin)
            // Base spins that trigger the feature are NOT respins - they still cost the bet
            var isRespin = isRespinBeforeSpin;
            var betToDeduct = isRespin ? 0m : totalBet.Amount;
            
            // Update balance: deduct bet (0 for respins), add win
            sessions.UpdateBalance(session.SessionId, betToDeduct, engineResponse.Win.Amount);
            
            // Get updated balance (session reference is updated since SessionRecord is a class)
            var balance = session.Balance;
            
            var maxWinCap = 0m; // TODO: Get from game configuration
            var currencyId = "ZAR"; // TODO: Get from session/player
            
            logger.LogInformation("Balance updated: PrevBalance={PrevBalance}, Bet={Bet}, Win={Win}, NewBalance={NewBalance}, IsRespin={IsRespin}", 
                prevBalance, betToDeduct, engineResponse.Win.Amount, balance, isRespin);

            // Log balance update
            GameLogger.LogBalanceUpdate(
                session.SessionId,
                prevBalance,
                betToDeduct,
                engineResponse.Win.Amount,
                balance);

            // Log engine response
            var respinsRemaining = engineResponse.NextState.Respins?.RespinsRemaining;
            var featureEnded = respinsRemaining.HasValue && respinsRemaining.Value == 0;
            var gridLayout = FormatGridLayout(engineResponse.Results);
            var winsInfo = FormatWinsInfo(engineResponse.Results.Wins);
            
            // Check if locked reels are preserved in the grid (for respins)
            if (isRespinBeforeSpin && engineResponse.NextState.Respins != null && engineResponse.NextState.Respins.LockedWildReels.Count > 0)
            {
                var lockedReelsCheck = CheckLockedReelsInGrid(engineResponse.Results.FinalGridSymbols, engineResponse.NextState.Respins.LockedWildReels);
                GameLogger.LogInfo($"RESPIN GRID CHECK - {lockedReelsCheck}");
            }
            
            GameLogger.LogEngineResponse(
                engineResponse.RoundId,
                engineResponse.Win.Amount,
                respinsRemaining,
                featureEnded,
                gridLayout,
                winsInfo);

            // Transform engine response to client-compliant format
            var clientResponse = ResponseTransformer.ToClientPlayResponse(
                engineResponse: engineResponse,
                sessionId: session.SessionId,
                prevBalance: prevBalance,
                balance: balance,
                bet: betToDeduct, // Use betToDeduct (0 for respins, actual bet for base spins)
                currencyId: currencyId,
                maxWinCap: maxWinCap);

            // Log RGS response
            GameLogger.LogRgsResponse(
                engineResponse.RoundId,
                balance,
                clientResponse.Feature.Name,
                clientResponse.Feature.Type,
                clientResponse.Feature.IsClosure,
                clientResponse.Game.Mode);

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
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ErrorCodes.GetMessage(ErrorCodes.BadRequest) }, statusCode: 200);
            }

            if (!sessions.TryGetSession(request.SessionId, out var session))
            {
                return Results.Json(new { statusCode = ErrorCodes.Unauthorized, message = ErrorCodes.GetMessage(ErrorCodes.Unauthorized) }, statusCode: 200);
            }

            if (!string.Equals(session.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Session does not match game." }, statusCode: 200);
            }

            if (!TryParseBetMode(request.BetMode, out var betMode))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Unknown betMode." }, statusCode: 200);
            }

            if (betMode != BetMode.Standard)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "ANTE_MODE_BUY_NOT_ALLOWED" }, statusCode: 200);
            }

            Money baseBet;
            try
            {
                baseBet = new Money(request.BaseBet);
            }
            catch (Exception ex)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = $"Invalid baseBet value: {ex.Message}" }, statusCode: 200);
            }

            var totalBet = CalculateTotalBet(baseBet, betMode);
            if (totalBet.Amount <= 0)
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "Total bet must be positive." }, statusCode: 200);
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
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ex.Message }, statusCode: 200);
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
            
            // Update balance: deduct buy cost, add win
            if (engineResponse.BuyCost.Amount > 0)
            {
                var prevBalance = session.Balance;
                sessions.UpdateBalance(session.SessionId, engineResponse.BuyCost.Amount, engineResponse.Win.Amount);
                var newBalance = sessions.GetBalance(session.SessionId);
                logger.LogInformation("Buy feature: PrevBalance={PrevBalance}, BuyCost={BuyCost}, Win={Win}, NewBalance={NewBalance}", 
                    prevBalance, engineResponse.BuyCost.Amount, engineResponse.Win.Amount, newBalance);
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
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = ErrorCodes.GetMessage(ErrorCodes.BadRequest) }, statusCode: 200);
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return Results.Json(new { statusCode = ErrorCodes.BadRequest, message = "playerId is required." }, statusCode: 200);
            }

            // TODO: Get actual balance from player service/database
            // For now, try to find session and return demo balance
            var balance = 10000m; // TODO: Get from player service using request.PlayerId
            
            var response = new ClientBalanceResponse(
                StatusCode: ErrorCodes.BalanceOK,
                Message: ErrorCodes.GetMessage(ErrorCodes.BalanceOK),
                Balance: balance);

            return Results.Ok(response);
        })
    .WithName("GetBalance");

app.Run();

static string FormatWinsInfo(IReadOnlyList<SymbolWin> wins)
{
    if (wins == null || wins.Count == 0)
    {
        return null;
    }

    // Payline definitions for logging
    var paylineDefinitions = new[]
    {
        new[] { 1, 1, 1, 1, 1 }, // Payline 1: Middle row
        new[] { 0, 0, 0, 0, 0 }, // Payline 2: Top row
        new[] { 2, 2, 2, 2, 2 }, // Payline 3: Bottom row
        new[] { 0, 1, 2, 1, 0 }, // Payline 4: V-shape up
        new[] { 2, 1, 0, 1, 2 }, // Payline 5: V-shape down
        new[] { 0, 0, 1, 0, 0 }, // Payline 6: Top-center
        new[] { 2, 2, 1, 2, 2 }, // Payline 7: Bottom-center
        new[] { 1, 2, 2, 2, 1 }, // Payline 8: Bottom-heavy
        new[] { 1, 0, 0, 0, 1 }, // Payline 9: Top-heavy
        new[] { 1, 0, 1, 0, 1 }  // Payline 10: Alternating
    };

    var sb = new System.Text.StringBuilder();
    foreach (var win in wins)
    {
        var paylineInfo = "";
        if (win.PaylineId.HasValue && win.PaylineId.Value >= 1 && win.PaylineId.Value <= paylineDefinitions.Length)
        {
            var paylineDef = paylineDefinitions[win.PaylineId.Value - 1];
            paylineInfo = $" - Payline {win.PaylineId.Value} [{string.Join(",", paylineDef)}]";
        }
        sb.AppendLine($"    {win.SymbolCode} x{win.Count} = {win.Payout.Amount:F2} (Multiplier: {win.Multiplier}x){paylineInfo}");
    }
    
    return sb.ToString();
}

static string FormatGridLayout(ResultsEnvelope results)
{
    if (results.FinalGridSymbols == null || results.FinalGridSymbols.Count == 0)
    {
        return "  (No grid data)";
    }

    // Assume 5x3 grid (standard for Starburst)
    const int cols = 5;
    const int rows = 3;
    var grid = results.FinalGridSymbols;
    
    // Symbol ID to name mapping (based on standard Starburst symbol catalog order)
    // ID 0 = WILD, ID 1 = BAR, ID 2 = SEVEN, ID 3 = RED, ID 4 = PURPLE, ID 5 = BLUE, ID 6 = GREEN, ID 7 = ORANGE
    var symbolNameMap = new Dictionary<int, string>
    {
        { 0, "WILD" },
        { 1, "BAR" },
        { 2, "SEVEN" },
        { 3, "RED" },
        { 4, "PURPLE" },
        { 5, "BLUE" },
        { 6, "GREEN" },
        { 7, "ORANGE" }
    };
    
    var sb = new System.Text.StringBuilder();
    
    // Readable format
    sb.AppendLine("  --- Grid Layout (Readable Format) ---");
    for (int displayRow = rows - 1; displayRow >= 0; displayRow--)
    {
        var rowSymbols = new List<string>();
        for (int col = 0; col < cols; col++)
        {
            var flatIndex = displayRow * cols + col;
            if (flatIndex < grid.Count)
            {
                var symbolId = grid[flatIndex];
                var symbolName = symbolNameMap.TryGetValue(symbolId, out var name) ? name : $"ID{symbolId}";
                rowSymbols.Add($"{symbolName}({symbolId})");
            }
            else
            {
                rowSymbols.Add("EMPTY");
            }
        }
        var rowLabel = displayRow == rows - 1 ? "TOP" : displayRow == 0 ? "BOT" : "MID";
        sb.AppendLine($"    {rowLabel} ROW: [{string.Join(" | ", rowSymbols)}]");
    }
    
    // Column view (reels)
    sb.AppendLine("  --- Column View (Reels) ---");
    for (int col = 0; col < cols; col++)
    {
        var colSymbols = new List<string>();
        for (int displayRow = rows - 1; displayRow >= 0; displayRow--)
        {
            var flatIndex = displayRow * cols + col;
            if (flatIndex < grid.Count)
            {
                var symbolId = grid[flatIndex];
                var symbolName = symbolNameMap.TryGetValue(symbolId, out var name) ? name : $"ID{symbolId}";
                colSymbols.Add($"{symbolName}({symbolId})");
            }
            else
            {
                colSymbols.Add("EMPTY");
            }
        }
        sb.AppendLine($"    REEL {col + 1}: [{string.Join(" | ", colSymbols)}]");
    }
    
    // ID format for reference
    sb.AppendLine("  --- Grid Layout (ID Format) ---");
    for (int displayRow = rows - 1; displayRow >= 0; displayRow--)
    {
        var rowSymbols = new List<string>();
        for (int col = 0; col < cols; col++)
        {
            var flatIndex = displayRow * cols + col;
            if (flatIndex < grid.Count)
            {
                rowSymbols.Add($"ID{grid[flatIndex]}");
            }
            else
            {
                rowSymbols.Add("EMPTY");
            }
        }
        var rowLabel = displayRow == rows - 1 ? "TOP" : displayRow == 0 ? "BOT" : "MID";
        sb.AppendLine($"    {rowLabel} ROW: [{string.Join(" | ", rowSymbols)}]");
    }
    
    return sb.ToString();
}

static string CheckLockedReelsInGrid(IReadOnlyList<int>? grid, HashSet<int> lockedReels)
{
    if (grid == null || grid.Count == 0 || lockedReels == null || lockedReels.Count == 0)
    {
        return "No grid or locked reels to check";
    }
    
    const int cols = 5;
    const int rows = 3;
    const int WILD_ID = 0;
    
    var issues = new List<string>();
    var successes = new List<string>();
    
    foreach (var lockedReel in lockedReels)
    {
        if (lockedReel < 0 || lockedReel >= cols)
        {
            issues.Add($"Invalid locked reel index: {lockedReel}");
            continue;
        }
        
        // Check all rows for this reel
        // Grid is flattened bottom-to-top: [row2reel0, row2reel1, ..., row1reel0, ..., row0reel0, ...]
        // For row r (0=top, 1=middle, 2=bottom) and reel c: index = (rows - 1 - r) * cols + c
        var reelSymbols = new List<int>();
        var allWilds = true;
        
        for (int row = 0; row < rows; row++)
        {
            var index = (rows - 1 - row) * cols + lockedReel;
            if (index < grid.Count)
            {
                var symbolId = grid[index];
                reelSymbols.Add(symbolId);
                if (symbolId != WILD_ID)
                {
                    allWilds = false;
                }
            }
        }
        
        if (allWilds && reelSymbols.Count == rows)
        {
            successes.Add($"Reel {lockedReel + 1}: All wilds ✓ [{string.Join(",", reelSymbols)}]");
        }
        else
        {
            issues.Add($"Reel {lockedReel + 1}: NOT all wilds ✗ [{string.Join(",", reelSymbols)}] (expected all {WILD_ID})");
        }
    }
    
    var result = new System.Text.StringBuilder();
    result.AppendLine($"LockedReels check: {lockedReels.Count} reel(s) to verify");
    if (successes.Count > 0)
    {
        foreach (var success in successes)
        {
            result.AppendLine($"  ✓ {success}");
        }
    }
    if (issues.Count > 0)
    {
        foreach (var issue in issues)
        {
            result.AppendLine($"  ✗ {issue}");
        }
    }
    
    return result.ToString();
}

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
