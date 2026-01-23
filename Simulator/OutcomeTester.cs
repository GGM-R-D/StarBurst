using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;

namespace Simulator;

/// <summary>
/// Interactive outcome tester that allows users to select specific payline/symbol/count
/// combinations and verifies they can be achieved with the current reel strips.
/// </summary>
public sealed class OutcomeTester
{
    private readonly GameConfigurationLoader _configLoader;
    private readonly string _configPath;
    private readonly HttpClient _httpClient;
    private BrowserController? _browserController;
    private string? _rgsBaseUrl = "http://localhost:5101";
    private string? _currentSessionId;
    private decimal _sessionBalance = 10000m;
    private bool _browserConnected = false;

    // Payline definitions matching WinEvaluator
    private static readonly int[][] Paylines = new[]
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

    private static readonly string[] PaylineDescriptions = new[]
    {
        "Middle row (straight)",
        "Top row (straight)",
        "Bottom row (straight)",
        "V-shape up",
        "V-shape down (inverted V)",
        "Top-center",
        "Bottom-center",
        "Bottom-heavy",
        "Top-heavy",
        "Alternating"
    };

    // Symbol definitions
    private static readonly (string Sym, string Code, string Name)[] Symbols = new[]
    {
        ("Sym1", "WILD", "Wild"),
        ("Sym2", "BAR", "Bar"),
        ("Sym3", "SEVEN", "Seven"),
        ("Sym4", "RED", "Red Gem"),
        ("Sym5", "PURPLE", "Purple Gem"),
        ("Sym6", "BLUE", "Blue Gem"),
        ("Sym7", "GREEN", "Green Gem"),
        ("Sym8", "ORANGE", "Orange Gem")
    };

    public OutcomeTester(string configPath)
    {
        _configPath = configPath;
        _configLoader = new GameConfigurationLoader(configPath);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task RunInteractiveMode()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                      OUTCOME TESTER - INTERACTIVE MODE                       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Load configuration
        GameConfiguration config;
        try
        {
            config = await _configLoader.GetConfigurationAsync("starburst");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to load configuration: {ex.Message}");
            return;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("                              MAIN MENU                                        ");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine();
            Console.WriteLine($"  RGS URL:    {_rgsBaseUrl}");
            Console.WriteLine($"  Session:    {(_currentSessionId != null ? $"{_currentSessionId} (Balance: R{_sessionBalance:N2})" : "Not connected")}");
            Console.WriteLine($"  Browser:    {(_browserConnected ? "✓ Connected" : "Not connected")}");
            Console.WriteLine();
            Console.WriteLine("  ─── BROWSER CONTROL (Spin from Console!) ───");
            Console.WriteLine("  1. Test specific payline win (SPINS THE GAME!)");
            Console.WriteLine("  2. Connect to Browser (Chrome)");
            Console.WriteLine();
            Console.WriteLine("  ─── RGS SESSION ───");
            Console.WriteLine("  3. Connect to RGS / Start new session");
            Console.WriteLine("  4. Use existing session (paste SessionId)");
            Console.WriteLine();
            Console.WriteLine("  ─── LOCAL SIMULATION ───");
            Console.WriteLine("  5. Test specific payline win (LOCAL - simulation only)");
            Console.WriteLine();
            Console.WriteLine("  ─── MULTI-PAYLINE TESTING ───");
            Console.WriteLine("  6. Test MULTIPLE paylines winning at once (BROWSER)");
            Console.WriteLine("  7. Test MULTIPLE paylines winning at once (LOCAL)");
            Console.WriteLine();
            Console.WriteLine("  ─── INFORMATION ───");
            Console.WriteLine("  8. Show all paylines");
            Console.WriteLine("  9. Show all symbols with paytable");
            Console.WriteLine("  10. Show reel strips");
            Console.WriteLine();
            Console.WriteLine("  0. Exit");
            Console.WriteLine();
            Console.Write("  Select option: ");

            var input = Console.ReadLine()?.Trim();
            
            switch (input)
            {
                case "1":
                    await TestSpecificWinBrowser(config);
                    break;
                case "2":
                    await ConnectToBrowser();
                    break;
                case "3":
                    await ConnectToRgs();
                    break;
                case "4":
                    UseExistingSession();
                    break;
                case "5":
                    await TestSpecificWin(config);
                    break;
                case "6":
                    await TestMultiPaylineWinBrowser(config);
                    break;
                case "7":
                    await TestMultiPaylineWinLocal(config);
                    break;
                case "8":
                    ShowAllPaylines();
                    break;
                case "9":
                    ShowAllSymbols(config);
                    break;
                case "10":
                    ShowReelStrips(config);
                    break;
                case "0":
                    Console.WriteLine("\n  Exiting outcome tester...\n");
                    if (_browserController != null)
                    {
                        await _browserController.DisposeAsync();
                    }
                    return;
                default:
                    Console.WriteLine("\n  Invalid option. Please try again.");
                    break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // BROWSER CONTROL METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task ConnectToBrowser()
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                        LAUNCH BROWSER                                          ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("  This will launch Chrome and open the game automatically.");
        Console.WriteLine("  (Make sure the frontend server is running at localhost:3030)");
        Console.WriteLine();

        _browserController ??= new BrowserController();
        
        // Use the new LaunchAsync method that handles everything automatically
        _browserConnected = await _browserController.LaunchAsync("http://localhost:3030/?funMode=1");

        if (_browserConnected)
        {
            Console.WriteLine();
            Console.WriteLine("  You can now use Option 1 to spin the game from this console!");
        }

        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private async Task TestSpecificWinBrowser(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                    TEST SPECIFIC WIN (BROWSER CONTROL)                         ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        // Auto-launch browser if not connected
        if (!_browserConnected || _browserController == null)
        {
            Console.WriteLine("  Browser not connected. Launching Chrome automatically...");
            Console.WriteLine();
            
            _browserController ??= new BrowserController();
            _browserConnected = await _browserController.LaunchAsync("http://localhost:3030/?funMode=1");
            
            if (!_browserConnected)
            {
                Console.WriteLine();
                Console.WriteLine("  ❌ Failed to launch browser. Make sure:");
                Console.WriteLine("     - The frontend server is running at localhost:3030");
                Console.WriteLine("     - Playwright browsers are installed (run: playwright install chromium)");
                Console.WriteLine();
                Console.WriteLine("  Press Enter to continue...");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();
        }

        Console.WriteLine($"  Browser: ✓ Connected");
        Console.WriteLine();

        // Select payline
        var paylineResult = SelectPayline();
        if (paylineResult == null) return;
        var (paylineNum, payline) = paylineResult.Value;

        // Select symbol
        var symbolResult = SelectSymbol(config);
        if (symbolResult == null) return;
        var selectedSymbol = symbolResult.Value;

        // Select match count
        var matchCountResult = SelectMatchCount(config, selectedSymbol);
        if (matchCountResult == null) return;
        var matchCount = matchCountResult.Value;

        // Find valid grid
        Console.WriteLine();
        Console.WriteLine("  STEP 4: Searching for Valid Grid...");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");

        var gridResult = FindValidGrid(config, paylineNum, selectedSymbol.Sym, matchCount);

        if (gridResult == null)
        {
            Console.WriteLine();
            Console.WriteLine($"  ❌ RESULT: Cannot achieve {matchCount}x {selectedSymbol.Code} win on Payline {paylineNum}");
            Console.WriteLine($"     with the current reel strips.");
            Console.WriteLine("\n  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Convert grid to symbol IDs (1-based for frontend)
        var customGridZeroBased = ConvertGridToSymbolIds(gridResult.Grid, config);
        var customGridOneBased = customGridZeroBased.Select(id => id + 1).ToArray();
        
        Console.WriteLine();
        Console.WriteLine($"  ✓ FOUND: Valid grid for {matchCount}x {selectedSymbol.Code} on Payline {paylineNum}");
        Console.WriteLine();

        // Show the reel strip positions being used (proves this is from the actual reelset)
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    REEL STRIP POSITIONS (From Reelset)                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Start positions: [{string.Join(", ", gridResult.ReelPositions)}]");
        Console.WriteLine();
        var reelStrips = config.ReelLibrary.High;
        for (int col = 0; col < config.Board.Columns; col++)
        {
            var strip = reelStrips[col];
            var startPos = gridResult.ReelPositions[col];
            var visibleSymbols = new List<string>();
            for (int row = 0; row < 3; row++)
            {
                var idx = (startPos + row) % strip.Count;
                var sym = strip[idx];
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym);
                visibleSymbols.Add(symDef?.Code ?? sym);
            }
            Console.WriteLine($"  Reel {col + 1}: Strip[{startPos}..{(startPos + 2) % strip.Count}] = [{string.Join(", ", visibleSymbols)}]");
        }
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  (These positions are taken directly from starburstReelsets.json)");
        Console.ResetColor();
        Console.WriteLine();

        // Display the grid visually
        DisplayGrid(gridResult.Grid, config, payline, paylineNum, matchCount);

        // Show win calculation
        var multiplier = GetMultiplier(config, selectedSymbol.Sym, matchCount);
        var betPerLine = 1.0m / 10; // R1 / 10 paylines
        var winAmount = betPerLine * multiplier;
        
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                           EXPECTED WIN                                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Symbol:        {selectedSymbol.Code} ({selectedSymbol.Name})");
        Console.WriteLine($"  Match Count:   {matchCount}x");
        Console.WriteLine($"  Payline:       {paylineNum} ({PaylineDescriptions[paylineNum - 1]})");
        Console.WriteLine($"  Multiplier:    {multiplier:F1}x");
        Console.WriteLine($"  Expected Win:  ~R{winAmount:F2} (may vary with both-ways pays)");
        Console.WriteLine();

        // Ask to spin
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                      READY TO SPIN THE GAME!                               │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("  Press ENTER to spin the game, or 'c' to cancel: ");
        
        var response = Console.ReadLine()?.Trim().ToLower();
        if (response == "c")
        {
            Console.WriteLine("  Cancelled.");
            Console.WriteLine("\n  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        Console.WriteLine();
        Console.WriteLine("  ─── SPINNING THE GAME ───");
        Console.WriteLine();

        var success = await _browserController.SetGridAndSpinAsync(customGridOneBased);

        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("  ✓ SPIN TRIGGERED!");
            Console.WriteLine("    Check your browser to see the result.");
            Console.ResetColor();
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // LIVE MODE METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task ConnectToRgs()
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                          CONNECT TO RGS                                        ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine($"  Current RGS URL: {_rgsBaseUrl}");
        Console.Write("  Enter new RGS URL (or press Enter to keep current): ");
        
        var newUrl = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(newUrl))
        {
            _rgsBaseUrl = newUrl.TrimEnd('/');
        }

        Console.WriteLine();
        Console.WriteLine("  Starting new session with funMode=1 (demo mode)...");

        try
        {
            var startRequest = new
            {
                funMode = 1,
                token = "SIMULATOR_TEST"
            };

            var response = await _httpClient.PostAsJsonAsync($"{_rgsBaseUrl}/test/starburst/start", startRequest);
            var responseText = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"\n  ❌ Failed to connect: {response.StatusCode}");
                Console.WriteLine($"     Response: {responseText}");
                Console.WriteLine("\n  Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var jsonDoc = JsonDocument.Parse(responseText);
            var root = jsonDoc.RootElement;

            // Try to extract sessionId from response
            if (root.TryGetProperty("sessionId", out var sessionIdProp))
            {
                _currentSessionId = sessionIdProp.GetString();
            }
            else if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("sessionId", out var dataSessionId))
            {
                _currentSessionId = dataSessionId.GetString();
            }

            // Try to extract balance
            if (root.TryGetProperty("balance", out var balanceProp))
            {
                _sessionBalance = balanceProp.GetDecimal();
            }
            else if (root.TryGetProperty("data", out var dataProp2) && dataProp2.TryGetProperty("balance", out var dataBalance))
            {
                _sessionBalance = dataBalance.GetDecimal();
            }

            if (_currentSessionId != null)
            {
                Console.WriteLine();
                Console.WriteLine($"  ✓ Connected successfully!");
                Console.WriteLine($"    Session ID: {_currentSessionId}");
                Console.WriteLine($"    Balance:    R{_sessionBalance:N2}");
                Console.WriteLine();
                Console.WriteLine("  You can now use option 1 to test specific outcomes on the live game!");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("  ⚠ Connected but could not extract session ID from response.");
                Console.WriteLine($"    Raw response: {responseText}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"\n  ❌ Connection failed: {ex.Message}");
            Console.WriteLine("     Make sure the RGS is running at the specified URL.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n  ❌ Error: {ex.Message}");
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }

    private void UseExistingSession()
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                       USE EXISTING SESSION                                     ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("  To find your session ID:");
        Console.WriteLine("  1. Open your browser's Developer Tools (F12)");
        Console.WriteLine("  2. Go to Network tab");
        Console.WriteLine("  3. Look for /play or /start requests");
        Console.WriteLine("  4. Find 'sessionId' in the request or response");
        Console.WriteLine();
        Console.Write("  Enter Session ID: ");

        var sessionId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(sessionId))
        {
            Console.WriteLine("\n  No session ID provided. Returning to menu.");
            return;
        }

        _currentSessionId = sessionId;
        Console.WriteLine();
        Console.WriteLine($"  ✓ Session ID set: {_currentSessionId}");
        Console.WriteLine("  Note: Make sure this session was started with funMode=1 for custom grids to work.");
        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private async Task TestSpecificWinLive(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                    TEST SPECIFIC WIN (LIVE MODE)                               ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine($"  Session: {_currentSessionId ?? "Not connected"}");
        Console.WriteLine();

        // Select payline
        var paylineResult = SelectPayline();
        if (paylineResult == null) return;
        var (paylineNum, payline) = paylineResult.Value;

        // Select symbol
        var symbolResult = SelectSymbol(config);
        if (symbolResult == null) return;
        var selectedSymbol = symbolResult.Value;

        // Select match count
        var matchCountResult = SelectMatchCount(config, selectedSymbol);
        if (matchCountResult == null) return;
        var matchCount = matchCountResult.Value;

        // Find valid grid
        Console.WriteLine();
        Console.WriteLine("  STEP 4: Searching for Valid Grid...");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");

        var gridResult = FindValidGrid(config, paylineNum, selectedSymbol.Sym, matchCount);

        if (gridResult == null)
        {
            Console.WriteLine();
            Console.WriteLine($"  ❌ RESULT: Cannot achieve {matchCount}x {selectedSymbol.Code} win on Payline {paylineNum}");
            Console.WriteLine($"     with the current reel strips.");
            Console.WriteLine("\n  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Convert grid to symbol IDs
        // 0-based for engine (0=WILD, 1=BAR, etc.)
        var customGridZeroBased = ConvertGridToSymbolIds(gridResult.Grid, config);
        // 1-based for frontend info panel (1=WILD, 2=BAR, etc.)  
        var customGridOneBased = customGridZeroBased.Select(id => id + 1).ToArray();
        
        Console.WriteLine();
        Console.WriteLine($"  ✓ FOUND: Valid grid for {matchCount}x {selectedSymbol.Code} on Payline {paylineNum}");
        Console.WriteLine();

        // Show the reel strip positions being used (proves this is from the actual reelset)
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    REEL STRIP POSITIONS (From Reelset)                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Start positions: [{string.Join(", ", gridResult.ReelPositions)}]");
        Console.WriteLine();
        var reelStrips = config.ReelLibrary.High;
        for (int col = 0; col < config.Board.Columns; col++)
        {
            var strip = reelStrips[col];
            var startPos = gridResult.ReelPositions[col];
            var visibleSymbols = new List<string>();
            for (int row = 0; row < 3; row++)
            {
                var idx = (startPos + row) % strip.Count;
                var sym = strip[idx];
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym);
                visibleSymbols.Add(symDef?.Code ?? sym);
            }
            Console.WriteLine($"  Reel {col + 1}: Strip[{startPos}..{(startPos + 2) % strip.Count}] = [{string.Join(", ", visibleSymbols)}]");
        }
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  (These positions are taken directly from starburstReelsets.json)");
        Console.ResetColor();
        Console.WriteLine();

        // Display the grid visually
        DisplayGrid(gridResult.Grid, config, payline, paylineNum, matchCount);

        // Show the grid arrays
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                      CUSTOM GRID FOR FRONTEND                              │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Grid Format (1-8 for Info Panel):");
        Console.WriteLine($"  [{string.Join(",", customGridOneBased)}]");
        Console.WriteLine();
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine("  HOW TO USE ON FRONTEND:");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("  OPTION A: Use Info Panel");
        Console.WriteLine("    1. Open the Info Panel on your game");
        Console.WriteLine("    2. Enter/paste the grid above into the custom grid input");
        Console.WriteLine("    3. Click Apply");
        Console.WriteLine("    4. Click Spin");
        Console.WriteLine();
        Console.WriteLine("  OPTION B: Browser Console (F12)");
        Console.WriteLine("    Copy and paste this command into the browser console:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"    sessionStorage.setItem('customFunModeGrid', '[{string.Join(",", customGridOneBased)}]');");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("    Then click Spin on the game.");
        Console.WriteLine();

        // Ask if user wants to also send via HTTP
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.Write("  Also send spin request via HTTP? (y/n): ");
        var sendHttp = Console.ReadLine()?.Trim().ToLower() == "y";

        if (sendHttp)
        {
            if (_currentSessionId == null)
            {
                Console.WriteLine();
                Console.WriteLine("  ❌ No session connected for HTTP request.");
                Console.WriteLine("     Use option 2 or 3 to connect a session first.");
            }
            else
            {
                await SendLiveSpinRequest(config, customGridZeroBased, paylineNum, selectedSymbol.Code, matchCount);
            }
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }

    private async Task SendLiveSpinRequest(GameConfiguration config, int[] customGrid, int paylineNum, string symbolCode, int matchCount)
    {
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                     SENDING TO LIVE GAME                                   │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        try
        {
            var playRequest = new
            {
                sessionId = _currentSessionId,
                baseBet = 1.0m,
                betMode = "standard",
                bets = new[] { new { betType = "main", amount = 1.0m } },
                userPayload = new { customFunModeGrid = customGrid }
            };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var requestJson = JsonSerializer.Serialize(playRequest, jsonOptions);
            Console.WriteLine("  Request:");
            Console.WriteLine($"  POST {_rgsBaseUrl}/test/starburst/play");
            Console.WriteLine();

            var response = await _httpClient.PostAsJsonAsync($"{_rgsBaseUrl}/test/starburst/play", playRequest);
            var responseText = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"  Response Status: {response.StatusCode}");
            Console.WriteLine();

            try
            {
                var jsonDoc = JsonDocument.Parse(responseText);
                var root = jsonDoc.RootElement;

                // Extract key values
                Console.WriteLine("  ─── RESULT ───");
                
                decimal winAmount = 0;
                if (root.TryGetProperty("win", out var winProp))
                {
                    winAmount = winProp.GetDecimal();
                }
                else if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("win", out var dataWin))
                {
                    winAmount = dataWin.GetDecimal();
                }
                
                if (root.TryGetProperty("balance", out var balProp))
                {
                    _sessionBalance = balProp.GetDecimal();
                }
                else if (root.TryGetProperty("data", out var dataProp2) && dataProp2.TryGetProperty("balance", out var dataBal))
                {
                    _sessionBalance = dataBal.GetDecimal();
                }

                Console.WriteLine($"  Win Amount:  R{winAmount:F2}");
                Console.WriteLine($"  New Balance: R{_sessionBalance:F2}");
                Console.WriteLine();
                Console.WriteLine($"  ✓ Spin executed for {matchCount}x {symbolCode} on Payline {paylineNum}!");
                Console.WriteLine();
                Console.WriteLine("  NOTE: The frontend won't auto-update from this HTTP call.");
                Console.WriteLine("        Use the sessionStorage method above for visual testing.");
            }
            catch
            {
                Console.WriteLine($"  Raw Response: {responseText}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"  ❌ Request failed: {ex.Message}");
            Console.WriteLine("     Make sure the RGS is running.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
        }
    }

    private (int paylineNum, int[] payline)? SelectPayline()
    {
        Console.WriteLine("  STEP 1: Select Payline");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        for (int i = 0; i < Paylines.Length; i++)
        {
            var rows = string.Join(",", Paylines[i]);
            Console.WriteLine($"    {i + 1,2}. Payline {i + 1}: [{rows}] - {PaylineDescriptions[i]}");
        }
        Console.WriteLine();
        Console.Write("  Enter payline number (1-10): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var paylineNum) || paylineNum < 1 || paylineNum > 10)
        {
            Console.WriteLine("\n  Invalid payline. Returning to menu.");
            return null;
        }

        return (paylineNum, Paylines[paylineNum - 1]);
    }

    private (string Sym, string Code, string Name)? SelectSymbol(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("  STEP 2: Select Symbol");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        
        var payingSymbols = Symbols.Where(s => s.Code != "WILD").ToArray();
        for (int i = 0; i < payingSymbols.Length; i++)
        {
            var sym = payingSymbols[i];
            var paytableEntry = config.Paytable.FirstOrDefault(p => p.SymbolCode == sym.Code);
            var payouts = paytableEntry != null 
                ? string.Join(", ", paytableEntry.Multipliers.Select(m => $"{m.Count}x={m.Multiplier}"))
                : "No payout";
            Console.WriteLine($"    {i + 1}. {sym.Code,-8} ({sym.Name}) - Payouts: {payouts}");
        }
        Console.WriteLine();
        Console.Write($"  Enter symbol number (1-{payingSymbols.Length}): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var symbolNum) || symbolNum < 1 || symbolNum > payingSymbols.Length)
        {
            Console.WriteLine("\n  Invalid symbol. Returning to menu.");
            return null;
        }

        return payingSymbols[symbolNum - 1];
    }

    private int? SelectMatchCount(GameConfiguration config, (string Sym, string Code, string Name) symbol)
    {
        Console.WriteLine();
        Console.WriteLine("  STEP 3: Select Match Count");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        
        var paytable = config.Paytable.FirstOrDefault(p => p.SymbolCode == symbol.Code);
        if (paytable == null)
        {
            Console.WriteLine($"\n  No paytable entry for {symbol.Code}. Returning to menu.");
            return null;
        }

        Console.WriteLine($"    Available match counts for {symbol.Code}:");
        foreach (var mult in paytable.Multipliers.OrderBy(m => m.Count))
        {
            Console.WriteLine($"      {mult.Count}x match = {mult.Multiplier}x payout");
        }
        Console.WriteLine();
        Console.Write("  Enter match count (3, 4, or 5): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var matchCount) || matchCount < 3 || matchCount > 5)
        {
            Console.WriteLine("\n  Invalid match count. Returning to menu.");
            return null;
        }

        return matchCount;
    }

    private int[] ConvertGridToSymbolIds(string[,] grid, GameConfiguration config)
    {
        // Grid is [row, col], need to convert to flat array in column-major order
        // Column-major: [reel0_row0, reel0_row1, reel0_row2, reel1_row0, ...]
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);
        var result = new int[rows * cols];

        for (int col = 0; col < cols; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                var symName = grid[row, col]; // e.g., "Sym4"
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == symName);
                var symbolIndex = symDef != null 
                    ? config.SymbolCatalog.ToList().IndexOf(symDef)
                    : 0;
                result[col * rows + row] = symbolIndex;
            }
        }

        return result;
    }

    private void DisplayGrid(string[,] grid, GameConfiguration config, int[] payline, int paylineNum, int matchCount)
    {
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);

        Console.WriteLine("       Reel 1    Reel 2    Reel 3    Reel 4    Reel 5");
        Console.WriteLine("      ─────────────────────────────────────────────────");

        string[] rowLabels = { "TOP", "MID", "BOT" };
        for (int row = 0; row < rows; row++)
        {
            Console.Write($"  {rowLabels[row]}  ");
            for (int col = 0; col < cols; col++)
            {
                var sym = grid[row, col];
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym);
                var code = symDef?.Code ?? sym;

                var isOnPayline = payline[col] == row;
                var isWinningPosition = isOnPayline && col < matchCount;

                if (isWinningPosition)
                {
                    Console.Write($" [{code,-6}] ");
                }
                else if (isOnPayline)
                {
                    Console.Write($" ({code,-6}) ");
                }
                else
                {
                    Console.Write($"  {code,-6}  ");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("      ─────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine($"  Legend: [SYMBOL] = Winning position on payline {paylineNum}");
        Console.WriteLine($"          (SYMBOL) = Non-winning position on payline {paylineNum}");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // LOCAL SIMULATION METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task TestSpecificWin(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                      TEST SPECIFIC WIN (LOCAL)                                 ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // Step 1: Select payline
        Console.WriteLine();
        Console.WriteLine("  STEP 1: Select Payline");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        for (int i = 0; i < Paylines.Length; i++)
        {
            var rows = string.Join(",", Paylines[i]);
            Console.WriteLine($"    {i + 1,2}. Payline {i + 1}: [{rows}] - {PaylineDescriptions[i]}");
        }
        Console.WriteLine();
        Console.Write("  Enter payline number (1-10): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var paylineNum) || paylineNum < 1 || paylineNum > 10)
        {
            Console.WriteLine("\n  Invalid payline. Returning to menu.");
            return;
        }

        var payline = Paylines[paylineNum - 1];

        // Step 2: Select symbol
        Console.WriteLine();
        Console.WriteLine("  STEP 2: Select Symbol");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        
        // Show paying symbols (exclude WILD since it doesn't pay directly)
        var payingSymbols = Symbols.Where(s => s.Code != "WILD").ToArray();
        for (int i = 0; i < payingSymbols.Length; i++)
        {
            var sym = payingSymbols[i];
            var paytableEntry = config.Paytable.FirstOrDefault(p => p.SymbolCode == sym.Code);
            var payouts = paytableEntry != null 
                ? string.Join(", ", paytableEntry.Multipliers.Select(m => $"{m.Count}x={m.Multiplier}"))
                : "No payout";
            Console.WriteLine($"    {i + 1}. {sym.Code,-8} ({sym.Name}) - Payouts: {payouts}");
        }
        Console.WriteLine();
        Console.Write($"  Enter symbol number (1-{payingSymbols.Length}): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var symbolNum) || symbolNum < 1 || symbolNum > payingSymbols.Length)
        {
            Console.WriteLine("\n  Invalid symbol. Returning to menu.");
            return;
        }

        var selectedSymbol = payingSymbols[symbolNum - 1];

        // Step 3: Select match count
        Console.WriteLine();
        Console.WriteLine("  STEP 3: Select Match Count");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        
        var paytable = config.Paytable.FirstOrDefault(p => p.SymbolCode == selectedSymbol.Code);
        if (paytable == null)
        {
            Console.WriteLine($"\n  No paytable entry for {selectedSymbol.Code}. Returning to menu.");
            return;
        }

        Console.WriteLine($"    Available match counts for {selectedSymbol.Code}:");
        foreach (var mult in paytable.Multipliers.OrderBy(m => m.Count))
        {
            Console.WriteLine($"      {mult.Count}x match = {mult.Multiplier}x payout");
        }
        Console.WriteLine();
        Console.Write("  Enter match count (3, 4, or 5): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var matchCount) || matchCount < 3 || matchCount > 5)
        {
            Console.WriteLine("\n  Invalid match count. Returning to menu.");
            return;
        }

        // Step 4: Find valid reel positions
        Console.WriteLine();
        Console.WriteLine("  STEP 4: Searching for Valid Grid...");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");

        var result = FindValidGrid(config, paylineNum, selectedSymbol.Sym, matchCount);

        if (result == null)
        {
            Console.WriteLine();
            Console.WriteLine($"  ❌ RESULT: Cannot achieve {matchCount}x {selectedSymbol.Code} win on Payline {paylineNum}");
            Console.WriteLine($"     with the current reel strips.");
            Console.WriteLine();
            Console.WriteLine("     This combination is NOT reachable - the reel strips do not contain");
            Console.WriteLine("     a valid sequence of symbols to produce this outcome.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"  ✓ FOUND: Valid grid for {matchCount}x {selectedSymbol.Code} on Payline {paylineNum}");
        Console.WriteLine();

        // Step 5: Execute the spin and show results
        await ExecuteAndShowResult(config, result, paylineNum, selectedSymbol, matchCount);
    }

    private GridSearchResult? FindValidGrid(GameConfiguration config, int paylineNum, string targetSym, int matchCount)
    {
        var payline = Paylines[paylineNum - 1];
        var reelStrips = config.ReelLibrary.High; // Use the base reel set
        var rows = config.Board.Rows;
        const string WILD_SYM = "Sym1"; // WILD symbol

        // We need to find reel start positions such that:
        // - For reels 0 to (matchCount-1): the symbol at payline row position is EXACTLY targetSym (NO WILD!)
        // - For reel matchCount (if matchCount < 5): the symbol should NOT be targetSym or WILD (to stop the win)
        // - NO WILD symbols anywhere in the entire grid (to prevent respin feature from triggering)

        // For each reel, build a list of valid start positions
        var validPositions = new List<List<int>>();

        for (int reelIdx = 0; reelIdx < config.Board.Columns; reelIdx++)
        {
            var strip = reelStrips[reelIdx];
            var targetRow = payline[reelIdx]; // Row position on this reel for the payline
            var validForReel = new List<int>();

            for (int startPos = 0; startPos < strip.Count; startPos++)
            {
                // First check: NO WILD symbols anywhere in the visible window (3 rows)
                bool hasWild = false;
                for (int row = 0; row < rows; row++)
                {
                    var idx = (startPos + row) % strip.Count;
                    if (strip[idx] == WILD_SYM)
                    {
                        hasWild = true;
                        break;
                    }
                }
                
                if (hasWild)
                {
                    continue; // Skip this position - it would show a WILD
                }

                // Calculate which symbol appears at targetRow when starting at startPos
                var symbolIdx = (startPos + targetRow) % strip.Count;
                var symbolAtRow = strip[symbolIdx];

                if (reelIdx < matchCount)
                {
                    // For winning reels: symbol must be EXACTLY the target (no WILD substitutes!)
                    if (symbolAtRow == targetSym)
                    {
                        validForReel.Add(startPos);
                    }
                }
                else if (reelIdx == matchCount && matchCount < 5)
                {
                    // For the reel after the win: symbol must NOT be target or WILD (to stop the win)
                    if (symbolAtRow != targetSym && symbolAtRow != WILD_SYM)
                    {
                        validForReel.Add(startPos);
                    }
                }
                else
                {
                    // Remaining reels: any position is fine (as long as no WILD, which we already checked)
                    validForReel.Add(startPos);
                }
            }

            validPositions.Add(validForReel);

            if (validForReel.Count == 0 && reelIdx <= matchCount)
            {
                // No valid position for a required reel
                return null;
            }
        }

        // Now pick one valid position from each reel
        var selectedPositions = new int[config.Board.Columns];
        for (int i = 0; i < config.Board.Columns; i++)
        {
            if (validPositions[i].Count > 0)
            {
                selectedPositions[i] = validPositions[i][0]; // Pick first valid
            }
            else
            {
                return null;
            }
        }

        // Build the grid
        var grid = new string[rows, config.Board.Columns];
        for (int col = 0; col < config.Board.Columns; col++)
        {
            var strip = reelStrips[col];
            for (int row = 0; row < rows; row++)
            {
                var idx = (selectedPositions[col] + row) % strip.Count;
                grid[row, col] = strip[idx];
            }
        }

        return new GridSearchResult
        {
            ReelPositions = selectedPositions,
            Grid = grid
        };
    }

    private async Task ExecuteAndShowResult(GameConfiguration config, GridSearchResult gridResult, int paylineNum, 
        (string Sym, string Code, string Name) symbol, int matchCount)
    {
        // Display the grid
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                            RESULT GRID                                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var payline = Paylines[paylineNum - 1];
        var grid = gridResult.Grid;
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);

        // Display grid with payline highlighted
        Console.WriteLine("       Reel 1    Reel 2    Reel 3    Reel 4    Reel 5");
        Console.WriteLine("      ─────────────────────────────────────────────────");

        string[] rowLabels = { "TOP", "MID", "BOT" };
        for (int row = 0; row < rows; row++)
        {
            Console.Write($"  {rowLabels[row]}  ");
            for (int col = 0; col < cols; col++)
            {
                var sym = grid[row, col];
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym);
                var code = symDef?.Code ?? sym;

                // Check if this position is on the selected payline
                var isOnPayline = payline[col] == row;
                var isWinningPosition = isOnPayline && col < matchCount;

                if (isWinningPosition)
                {
                    Console.Write($" [{code,-6}] ");
                }
                else if (isOnPayline)
                {
                    Console.Write($" ({code,-6}) ");
                }
                else
                {
                    Console.Write($"  {code,-6}  ");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("      ─────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine($"  Legend: [SYMBOL] = Winning position on payline {paylineNum}");
        Console.WriteLine($"          (SYMBOL) = Non-winning position on payline {paylineNum}");
        Console.WriteLine();

        // Calculate and show the expected payout
        var paytable = config.Paytable.FirstOrDefault(p => p.SymbolCode == symbol.Code);
        var multiplier = paytable?.Multipliers.FirstOrDefault(m => m.Count == matchCount);
        
        if (multiplier != null)
        {
            var betPerLine = 1.00m / 10; // R1 bet divided by 10 paylines
            var payout = betPerLine * multiplier.Multiplier;
            
            Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │                            WIN CALCULATION                                 │");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine($"  Symbol:        {symbol.Code} ({symbol.Name})");
            Console.WriteLine($"  Match Count:   {matchCount}x");
            Console.WriteLine($"  Payline:       {paylineNum} ({PaylineDescriptions[paylineNum - 1]})");
            Console.WriteLine($"  Multiplier:    {multiplier.Multiplier}x");
            Console.WriteLine($"  Bet per line:  R{betPerLine:F2} (R1.00 / 10 lines)");
            Console.WriteLine($"  Win Amount:    R{payout:F2}");
            Console.WriteLine();
        }

        // Now actually execute the spin using the game engine
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                         GAME ENGINE EXECUTION                              │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        try
        {
            // Create a custom RNG client that returns our predetermined reel positions
            var predeterminedRng = new PredeterminedRngClient(gridResult.ReelPositions);
            var prng = new FortunaPrng();
            var timeService = new SimulatorTimeService();
            var telemetry = new NullTelemetrySink();
            var winEvaluator = new WinEvaluator();
            var configLoader = new GameConfigurationLoader(_configPath);

            var spinHandler = new SpinHandler(
                configLoader,
                winEvaluator,
                timeService,
                prng,
                predeterminedRng,
                telemetry);

            // Suppress console output from SpinHandler
            var originalOut = Console.Out;
            Console.SetOut(TextWriter.Null);

            var request = new PlayRequest(
                GameId: "starburst",
                PlayerToken: "OUTCOME-TEST",
                Bets: new[] { new BetRequest("main", new Money(1.00m)) },
                BaseBet: new Money(1.00m),
                TotalBet: new Money(1.00m),
                BetMode: BetMode.Standard,
                IsFeatureBuy: false,
                EngineState: null,
                UserPayload: null,
                LastResponse: null,
                Bet: new Money(1.00m),
                FunMode: false);

            var response = await spinHandler.PlayAsync(request, CancellationToken.None);

            Console.SetOut(originalOut);

            // Display results
            Console.WriteLine($"  Engine Result:");
            Console.WriteLine($"    Total Win:     R{response.Win.Amount:F2}");
            Console.WriteLine($"    Win Count:     {response.Results.Wins.Count}");
            Console.WriteLine();

            if (response.Results.Wins.Count > 0)
            {
                Console.WriteLine($"  Wins Breakdown:");
                foreach (var win in response.Results.Wins)
                {
                    Console.WriteLine($"    - {win.SymbolCode} x{win.Count} on Payline {win.PaylineId} = R{win.Payout.Amount:F2} ({win.Multiplier}x)");
                }
            }

            // Check for expanding wilds feature
            if (response.Feature != null && response.Feature.Active == true)
            {
                Console.WriteLine();
                Console.WriteLine($"  ⭐ EXPANDING WILDS TRIGGERED!");
                Console.WriteLine($"     Locked Reels: {string.Join(", ", response.Feature.LockedReels?.Select(r => r + 1) ?? Array.Empty<int>())}");
                Console.WriteLine($"     Respins Awarded: {response.Feature.RespinsAwarded}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error executing spin: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // MULTI-PAYLINE TESTING METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    private async Task TestMultiPaylineWinBrowser(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("              TEST MULTIPLE PAYLINES WINNING (BROWSER CONTROL)                  ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("  This mode lets you configure multiple paylines to win simultaneously.");
        Console.WriteLine("  The simulator will find a grid where ALL specified wins occur at once.");
        Console.WriteLine();

        // Auto-launch browser if not connected
        if (!_browserConnected || _browserController == null)
        {
            Console.WriteLine("  Browser not connected. Launching Chrome automatically...");
            Console.WriteLine();
            
            _browserController ??= new BrowserController();
            _browserConnected = await _browserController.LaunchAsync("http://localhost:3030/?funMode=1");
            
            if (!_browserConnected)
            {
                Console.WriteLine();
                Console.WriteLine("  ❌ Failed to launch browser. Make sure:");
                Console.WriteLine("     - The frontend server is running at localhost:3030");
                Console.WriteLine("     - Playwright browsers are installed (run: playwright install chromium)");
                Console.WriteLine();
                Console.WriteLine("  Press Enter to continue...");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();
        }

        Console.WriteLine($"  Browser: ✓ Connected");
        Console.WriteLine();

        // Collect multi-payline targets
        var targets = SelectMultiplePaylineTargets(config);
        if (targets == null || targets.Count == 0)
        {
            Console.WriteLine("\n  No payline targets configured. Returning to menu.");
            Console.WriteLine("  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Try to find a valid grid
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    SEARCHING FOR VALID GRID                                 │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Attempting to find a grid where {targets.Count} payline(s) win simultaneously...");
        Console.WriteLine();

        var gridResult = FindMultiLineValidGrid(config, targets);

        if (gridResult == null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ❌ IMPOSSIBLE CONFIGURATION");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  The requested combination cannot be achieved with the current reel strips.");
            Console.WriteLine("  This could be because:");
            Console.WriteLine("    - The symbol doesn't exist on required reels");
            Console.WriteLine("    - Conflicting requirements (same position needs different symbols)");
            Console.WriteLine("    - No reel positions satisfy all constraints without WILD symbols");
            Console.WriteLine();
            Console.WriteLine("  Try a different combination of paylines, symbols, or match counts.");
            Console.WriteLine();
            Console.WriteLine("  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Convert grid
        var customGridZeroBased = ConvertGridToSymbolIds(gridResult.Grid, config);
        var customGridOneBased = customGridZeroBased.Select(id => id + 1).ToArray();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ FOUND VALID GRID!");
        Console.ResetColor();
        Console.WriteLine();

        // Show the winning paylines
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                         CONFIGURED WINS                                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        decimal totalExpectedWin = 0;
        foreach (var target in targets)
        {
            var multiplier = GetMultiplier(config, target.SymbolSym, target.MatchCount);
            var betPerLine = 1.0m / 10;
            var winAmount = betPerLine * multiplier;
            totalExpectedWin += winAmount;
            
            var symDef = Symbols.FirstOrDefault(s => s.Sym == target.SymbolSym);
            Console.WriteLine($"    Payline {target.PaylineNum,2}: {symDef.Code,-8} x{target.MatchCount} = R{winAmount:F2} ({multiplier}x multiplier)");
        }
        Console.WriteLine($"    {"─────────────────────────────────────────────────────────",-60}");
        Console.WriteLine($"    Total Expected Win (before both-ways): ~R{totalExpectedWin:F2}");
        Console.WriteLine();

        // Show reel positions
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    REEL STRIP POSITIONS (From Reelset)                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Start positions: [{string.Join(", ", gridResult.ReelPositions)}]");
        Console.WriteLine();

        // Show the grid
        DisplayMultiPaylineGrid(gridResult.Grid, config, targets);

        // Spin confirmation
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                      READY TO SPIN THE GAME!                               │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("  Press ENTER to spin the game, or 'c' to cancel: ");

        var response = Console.ReadLine()?.Trim().ToLower();
        if (response == "c")
        {
            Console.WriteLine("  Cancelled.");
            Console.WriteLine("\n  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        Console.WriteLine();
        Console.WriteLine("  ─── SPINNING THE GAME ───");
        Console.WriteLine();

        var success = await _browserController.SetGridAndSpinAsync(customGridOneBased);

        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("  ✓ SPIN TRIGGERED!");
            Console.WriteLine("    Check your browser to see the result.");
            Console.ResetColor();
        }

        Console.WriteLine("\n  Press Enter to continue...");
        Console.ReadLine();
    }

    private async Task TestMultiPaylineWinLocal(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("               TEST MULTIPLE PAYLINES WINNING (LOCAL SIMULATION)                ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("  This mode lets you configure multiple paylines to win simultaneously.");
        Console.WriteLine("  The simulator will find a grid where ALL specified wins occur at once.");
        Console.WriteLine();

        // Collect multi-payline targets
        var targets = SelectMultiplePaylineTargets(config);
        if (targets == null || targets.Count == 0)
        {
            Console.WriteLine("\n  No payline targets configured. Returning to menu.");
            Console.WriteLine("  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Try to find a valid grid
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    SEARCHING FOR VALID GRID                                 │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Attempting to find a grid where {targets.Count} payline(s) win simultaneously...");
        Console.WriteLine();

        var gridResult = FindMultiLineValidGrid(config, targets);

        if (gridResult == null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ❌ IMPOSSIBLE CONFIGURATION");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  The requested combination cannot be achieved with the current reel strips.");
            Console.WriteLine("  This could be because:");
            Console.WriteLine("    - The symbol doesn't exist on required reels");
            Console.WriteLine("    - Conflicting requirements (same position needs different symbols)");
            Console.WriteLine("    - No reel positions satisfy all constraints without WILD symbols");
            Console.WriteLine();
            Console.WriteLine("  Try a different combination of paylines, symbols, or match counts.");
            Console.WriteLine();
            Console.WriteLine("  Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ FOUND VALID GRID!");
        Console.ResetColor();
        Console.WriteLine();

        // Show the winning paylines
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                         CONFIGURED WINS                                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        decimal totalExpectedWin = 0;
        foreach (var target in targets)
        {
            var multiplier = GetMultiplier(config, target.SymbolSym, target.MatchCount);
            var betPerLine = 1.0m / 10;
            var winAmount = betPerLine * multiplier;
            totalExpectedWin += winAmount;
            
            var symDef = Symbols.FirstOrDefault(s => s.Sym == target.SymbolSym);
            Console.WriteLine($"    Payline {target.PaylineNum,2}: {symDef.Code,-8} x{target.MatchCount} = R{winAmount:F2} ({multiplier}x multiplier)");
        }
        Console.WriteLine($"    {"─────────────────────────────────────────────────────────",-60}");
        Console.WriteLine($"    Total Expected Win (before both-ways): ~R{totalExpectedWin:F2}");
        Console.WriteLine();

        // Show reel positions
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                    REEL STRIP POSITIONS (From Reelset)                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine($"  Start positions: [{string.Join(", ", gridResult.ReelPositions)}]");
        Console.WriteLine();

        // Show the grid
        DisplayMultiPaylineGrid(gridResult.Grid, config, targets);

        // Execute with game engine
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                         GAME ENGINE EXECUTION                              │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        try
        {
            var predeterminedRng = new PredeterminedRngClient(gridResult.ReelPositions);
            var prng = new FortunaPrng();
            var timeService = new SimulatorTimeService();
            var telemetry = new NullTelemetrySink();
            var winEvaluator = new WinEvaluator();
            var configLoader = new GameConfigurationLoader(_configPath);

            var spinHandler = new SpinHandler(
                configLoader,
                winEvaluator,
                timeService,
                prng,
                predeterminedRng,
                telemetry);

            // Suppress console output from SpinHandler
            var originalOut = Console.Out;
            Console.SetOut(TextWriter.Null);

            var request = new PlayRequest(
                GameId: "starburst",
                PlayerToken: "MULTI-PAYLINE-TEST",
                Bets: new[] { new BetRequest("main", new Money(1.00m)) },
                BaseBet: new Money(1.00m),
                TotalBet: new Money(1.00m),
                BetMode: BetMode.Standard,
                IsFeatureBuy: false,
                EngineState: null,
                UserPayload: null,
                LastResponse: null,
                Bet: new Money(1.00m),
                FunMode: false);

            var engineResponse = await spinHandler.PlayAsync(request, CancellationToken.None);

            Console.SetOut(originalOut);

            Console.WriteLine($"  Engine Result:");
            Console.WriteLine($"    Total Win:     R{engineResponse.Win.Amount:F2}");
            Console.WriteLine($"    Win Count:     {engineResponse.Results.Wins.Count}");
            Console.WriteLine();

            if (engineResponse.Results.Wins.Count > 0)
            {
                Console.WriteLine($"  Wins Breakdown:");
                foreach (var win in engineResponse.Results.Wins)
                {
                    Console.WriteLine($"    - {win.SymbolCode} x{win.Count} on Payline {win.PaylineId} = R{win.Payout.Amount:F2} ({win.Multiplier}x)");
                }
            }

            if (engineResponse.Feature != null && engineResponse.Feature.Active == true)
            {
                Console.WriteLine();
                Console.WriteLine($"  ⭐ EXPANDING WILDS TRIGGERED!");
                Console.WriteLine($"     Locked Reels: {string.Join(", ", engineResponse.Feature.LockedReels?.Select(r => r + 1) ?? Array.Empty<int>())}");
                Console.WriteLine($"     Respins Awarded: {engineResponse.Feature.RespinsAwarded}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error executing spin: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private List<PaylineTarget>? SelectMultiplePaylineTargets(GameConfiguration config)
    {
        var targets = new List<PaylineTarget>();
        var payingSymbols = Symbols.Where(s => s.Code != "WILD").ToArray();

        // Step 1: How many paylines?
        Console.WriteLine("  STEP 1: How many paylines should win?");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.Write("  Enter number of paylines (1-10): ");

        if (!int.TryParse(Console.ReadLine()?.Trim(), out var numPaylines) || numPaylines < 1 || numPaylines > 10)
        {
            Console.WriteLine("\n  Invalid number. Must be between 1 and 10.");
            return null;
        }

        // Step 2: Configure each payline
        Console.WriteLine();
        Console.WriteLine($"  STEP 2: Configure each of the {numPaylines} winning payline(s)");
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine();

        // Show all paylines for reference
        Console.WriteLine("  Available paylines:");
        for (int i = 0; i < Paylines.Length; i++)
        {
            var rows = string.Join(",", Paylines[i]);
            Console.WriteLine($"    {i + 1,2}. [{rows}] - {PaylineDescriptions[i]}");
        }
        Console.WriteLine();

        // Show all symbols for reference
        Console.WriteLine("  Available symbols:");
        for (int i = 0; i < payingSymbols.Length; i++)
        {
            var sym = payingSymbols[i];
            Console.WriteLine($"    {i + 1}. {sym.Code,-8} ({sym.Name})");
        }
        Console.WriteLine();

        var usedPaylines = new HashSet<int>();

        for (int n = 0; n < numPaylines; n++)
        {
            Console.WriteLine($"  ─── WIN #{n + 1} of {numPaylines} ───");
            Console.WriteLine();

            // Select payline
            int paylineNum;
            while (true)
            {
                Console.Write($"    Payline number (1-10): ");
                if (!int.TryParse(Console.ReadLine()?.Trim(), out paylineNum) || paylineNum < 1 || paylineNum > 10)
                {
                    Console.WriteLine("      Invalid payline. Try again.");
                    continue;
                }
                if (usedPaylines.Contains(paylineNum))
                {
                    Console.WriteLine($"      Payline {paylineNum} already configured. Choose a different one.");
                    continue;
                }
                break;
            }
            usedPaylines.Add(paylineNum);

            // Select symbol
            Console.Write($"    Symbol number (1-{payingSymbols.Length}): ");
            if (!int.TryParse(Console.ReadLine()?.Trim(), out var symbolNum) || symbolNum < 1 || symbolNum > payingSymbols.Length)
            {
                Console.WriteLine("      Invalid symbol. Aborting.");
                return null;
            }
            var selectedSymbol = payingSymbols[symbolNum - 1];

            // Select match count
            Console.Write($"    Match count (3, 4, or 5): ");
            if (!int.TryParse(Console.ReadLine()?.Trim(), out var matchCount) || matchCount < 3 || matchCount > 5)
            {
                Console.WriteLine("      Invalid match count. Aborting.");
                return null;
            }

            targets.Add(new PaylineTarget
            {
                PaylineNum = paylineNum,
                SymbolSym = selectedSymbol.Sym,
                SymbolCode = selectedSymbol.Code,
                MatchCount = matchCount
            });

            Console.WriteLine($"      ✓ Added: Payline {paylineNum} with {selectedSymbol.Code} x{matchCount}");
            Console.WriteLine();
        }

        // Summary
        Console.WriteLine("  ─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine("  CONFIGURATION SUMMARY:");
        foreach (var t in targets)
        {
            Console.WriteLine($"    - Payline {t.PaylineNum}: {t.SymbolCode} x{t.MatchCount}");
        }
        Console.WriteLine();
        Console.Write("  Proceed with this configuration? (y/n): ");
        
        if (Console.ReadLine()?.Trim().ToLower() != "y")
        {
            return null;
        }

        return targets;
    }

    private GridSearchResult? FindMultiLineValidGrid(GameConfiguration config, List<PaylineTarget> targets)
    {
        var reelStrips = config.ReelLibrary.High;
        var rows = config.Board.Rows;
        var cols = config.Board.Columns;
        const string WILD_SYM = "Sym1";

        // Step 1: Build constraint map
        // Key: (reel, row), Value: required symbol (or null if no constraint)
        var constraints = new Dictionary<(int reel, int row), string>();
        var breakerConstraints = new Dictionary<(int reel, int row), string>(); // Positions that must NOT match

        foreach (var target in targets)
        {
            var payline = Paylines[target.PaylineNum - 1];

            // Winning positions: must have the target symbol
            for (int reel = 0; reel < target.MatchCount; reel++)
            {
                var row = payline[reel];
                var key = (reel, row);

                if (constraints.TryGetValue(key, out var existingSymbol))
                {
                    // Check for conflict
                    if (existingSymbol != target.SymbolSym)
                    {
                        Console.WriteLine($"    CONFLICT at Reel {reel + 1}, Row {row}: ");
                        Console.WriteLine($"      Requires both {existingSymbol} and {target.SymbolSym}");
                        return null;
                    }
                }
                else
                {
                    constraints[key] = target.SymbolSym;
                }
            }

            // Breaker position: must NOT have target symbol (to stop the win at exactly matchCount)
            if (target.MatchCount < cols)
            {
                var breakerReel = target.MatchCount;
                var breakerRow = payline[breakerReel];
                var breakerKey = (breakerReel, breakerRow);

                // Check if this position already has a winning constraint
                if (constraints.TryGetValue(breakerKey, out var winningSymbol))
                {
                    // If the winning symbol is the same as what we need to break, it's a conflict
                    if (winningSymbol == target.SymbolSym)
                    {
                        Console.WriteLine($"    CONFLICT at Reel {breakerReel + 1}, Row {breakerRow}: ");
                        Console.WriteLine($"      Payline {target.PaylineNum} needs to STOP here, but another payline needs {winningSymbol} here");
                        return null;
                    }
                    // Otherwise the winning symbol will naturally break this payline
                }
                else
                {
                    // Add breaker constraint
                    if (!breakerConstraints.ContainsKey(breakerKey))
                    {
                        breakerConstraints[breakerKey] = target.SymbolSym; // Must NOT be this symbol
                    }
                }
            }
        }

        Console.WriteLine($"  Constraints: {constraints.Count} positions must match specific symbols");
        Console.WriteLine($"  Breakers: {breakerConstraints.Count} positions must NOT match specific symbols");

        // Step 2: For each reel, determine required rows and their symbols
        var reelRequirements = new Dictionary<int, Dictionary<int, string>>();
        var reelBreakers = new Dictionary<int, Dictionary<int, string>>();

        for (int reel = 0; reel < cols; reel++)
        {
            reelRequirements[reel] = new Dictionary<int, string>();
            reelBreakers[reel] = new Dictionary<int, string>();
        }

        foreach (var (key, symbol) in constraints)
        {
            reelRequirements[key.reel][key.row] = symbol;
        }

        foreach (var (key, symbol) in breakerConstraints)
        {
            // Only add breaker if there's no winning constraint for this position
            if (!constraints.ContainsKey(key))
            {
                reelBreakers[key.reel][key.row] = symbol;
            }
        }

        // Step 3: Find valid start positions for each reel
        var validPositions = new List<int>[cols];

        for (int reel = 0; reel < cols; reel++)
        {
            validPositions[reel] = new List<int>();
            var strip = reelStrips[reel];
            var requirements = reelRequirements[reel];
            var breakers = reelBreakers[reel];

            for (int startPos = 0; startPos < strip.Count; startPos++)
            {
                bool valid = true;

                // Check 1: NO WILD symbols in visible window
                for (int row = 0; row < rows; row++)
                {
                    var idx = (startPos + row) % strip.Count;
                    if (strip[idx] == WILD_SYM)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                // Check 2: All required rows have correct symbol
                foreach (var (row, requiredSym) in requirements)
                {
                    var idx = (startPos + row) % strip.Count;
                    if (strip[idx] != requiredSym)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                // Check 3: Breaker positions don't have the forbidden symbol
                foreach (var (row, forbiddenSym) in breakers)
                {
                    var idx = (startPos + row) % strip.Count;
                    if (strip[idx] == forbiddenSym || strip[idx] == WILD_SYM)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                {
                    validPositions[reel].Add(startPos);
                }
            }

            if (validPositions[reel].Count == 0)
            {
                Console.WriteLine($"    No valid positions for Reel {reel + 1}");
                return null;
            }
        }

        // Step 4: Pick first valid combination
        var selectedPositions = new int[cols];
        for (int reel = 0; reel < cols; reel++)
        {
            selectedPositions[reel] = validPositions[reel][0];
        }

        // Step 5: Build the grid
        var grid = new string[rows, cols];
        for (int col = 0; col < cols; col++)
        {
            var strip = reelStrips[col];
            for (int row = 0; row < rows; row++)
            {
                var idx = (selectedPositions[col] + row) % strip.Count;
                grid[row, col] = strip[idx];
            }
        }

        return new GridSearchResult
        {
            ReelPositions = selectedPositions,
            Grid = grid
        };
    }

    private void DisplayMultiPaylineGrid(string[,] grid, GameConfiguration config, List<PaylineTarget> targets)
    {
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                            RESULT GRID                                     │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);

        // Build a map of winning positions: (reel, row) -> list of payline numbers
        var winningPositions = new Dictionary<(int reel, int row), List<int>>();
        foreach (var target in targets)
        {
            var payline = Paylines[target.PaylineNum - 1];
            for (int reel = 0; reel < target.MatchCount; reel++)
            {
                var key = (reel, payline[reel]);
                if (!winningPositions.ContainsKey(key))
                    winningPositions[key] = new List<int>();
                winningPositions[key].Add(target.PaylineNum);
            }
        }

        Console.WriteLine("       Reel 1    Reel 2    Reel 3    Reel 4    Reel 5");
        Console.WriteLine("      ─────────────────────────────────────────────────");

        string[] rowLabels = { "TOP", "MID", "BOT" };
        for (int row = 0; row < rows; row++)
        {
            Console.Write($"  {rowLabels[row]}  ");
            for (int col = 0; col < cols; col++)
            {
                var sym = grid[row, col];
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym);
                var code = symDef?.Code ?? sym;

                var key = (col, row);
                if (winningPositions.TryGetValue(key, out var paylineNums))
                {
                    // This is a winning position
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($" [{code,-6}] ");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write($"  {code,-6}  ");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("      ─────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("  Legend: [SYMBOL] = Winning position");
        Console.WriteLine();
    }

    private sealed class PaylineTarget
    {
        public int PaylineNum { get; init; }
        public string SymbolSym { get; init; } = "";
        public string SymbolCode { get; init; } = "";
        public int MatchCount { get; init; }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // INFORMATION DISPLAY METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    private void ShowAllPaylines()
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                              ALL PAYLINES                                      ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("  Row indices: 0 = Top, 1 = Middle, 2 = Bottom");
        Console.WriteLine();

        for (int i = 0; i < Paylines.Length; i++)
        {
            var rows = Paylines[i];
            Console.WriteLine($"  Payline {i + 1,2}: [{rows[0]},{rows[1]},{rows[2]},{rows[3]},{rows[4]}] - {PaylineDescriptions[i]}");
            
            // Visual representation
            Console.WriteLine();
            for (int row = 0; row < 3; row++)
            {
                Console.Write("             ");
                for (int col = 0; col < 5; col++)
                {
                    Console.Write(rows[col] == row ? " ● " : " ○ ");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private void ShowAllSymbols(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                           ALL SYMBOLS & PAYTABLE                               ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        Console.WriteLine($"  {"ID",-6} {"Code",-8} {"Name",-15} {"Type",-10} {"3x",-8} {"4x",-8} {"5x",-8}");
        Console.WriteLine($"  {new string('-', 70)}");

        foreach (var sym in Symbols)
        {
            var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == sym.Sym);
            var paytable = config.Paytable.FirstOrDefault(p => p.SymbolCode == sym.Code);

            var type = symDef?.Type.ToString() ?? "Unknown";
            var pay3 = paytable?.Multipliers.FirstOrDefault(m => m.Count == 3)?.Multiplier.ToString("F0") ?? "-";
            var pay4 = paytable?.Multipliers.FirstOrDefault(m => m.Count == 4)?.Multiplier.ToString("F0") ?? "-";
            var pay5 = paytable?.Multipliers.FirstOrDefault(m => m.Count == 5)?.Multiplier.ToString("F0") ?? "-";

            Console.WriteLine($"  {sym.Sym,-6} {sym.Code,-8} {sym.Name,-15} {type,-10} {pay3 + "x",-8} {pay4 + "x",-8} {pay5 + "x",-8}");
        }

        Console.WriteLine();
        Console.WriteLine("  Note: WILD substitutes for all symbols but has no direct payout.");
        Console.WriteLine("        WILD can only appear on Reels 2, 3, and 4.");
        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private void ShowReelStrips(GameConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("                              REEL STRIPS                                       ");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        var reelStrips = config.ReelLibrary.High;

        for (int reelIdx = 0; reelIdx < reelStrips.Count; reelIdx++)
        {
            var strip = reelStrips[reelIdx];
            Console.WriteLine($"  Reel {reelIdx + 1} ({strip.Count} symbols):");
            
            // Count symbols on this reel
            var symbolCounts = strip.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
            
            Console.Write("    Symbols: ");
            var formattedCounts = symbolCounts.OrderBy(kv => kv.Key).Select(kv =>
            {
                var symDef = config.SymbolCatalog.FirstOrDefault(s => s.Sym == kv.Key);
                var code = symDef?.Code ?? kv.Key;
                return $"{code}={kv.Value}";
            });
            Console.WriteLine(string.Join(", ", formattedCounts));
            
            // Show first 10 positions as sample
            Console.Write("    Sample:  ");
            var sample = strip.Take(10).Select(s =>
            {
                var symDef = config.SymbolCatalog.FirstOrDefault(sd => sd.Sym == s);
                return symDef?.Code ?? s;
            });
            Console.WriteLine($"[{string.Join(", ", sample)}...]");
            Console.WriteLine();
        }

        Console.WriteLine("  Press Enter to continue...");
        Console.ReadLine();
    }

    private decimal GetMultiplier(GameConfiguration config, string symbolSym, int matchCount)
    {
        var symbol = config.SymbolCatalog.FirstOrDefault(s => s.Sym == symbolSym);
        if (symbol == null) return 0;
        
        var paytable = config.Paytable.FirstOrDefault(p => p.SymbolCode == symbol.Code);
        var multiplier = paytable?.Multipliers.FirstOrDefault(m => m.Count == matchCount);
        return multiplier?.Multiplier ?? 0;
    }

    private sealed class GridSearchResult
    {
        public int[] ReelPositions { get; init; } = Array.Empty<int>();
        public string[,] Grid { get; init; } = new string[0, 0];
    }
}

/// <summary>
/// RNG client that returns predetermined reel positions for outcome testing.
/// </summary>
public sealed class PredeterminedRngClient : RNGClient.IRngClient
{
    private readonly int[] _reelPositions;

    public PredeterminedRngClient(int[] reelPositions)
    {
        _reelPositions = reelPositions;
    }

    public Task<RNGClient.PoolsResponse> RequestPoolsAsync(RNGClient.JurisdictionPoolsRequest request, CancellationToken cancellationToken = default)
    {
        var pools = new List<RNGClient.PoolResult>();

        foreach (var poolRequest in request.Pools)
        {
            var results = new List<string>();

            if (poolRequest.PoolId == "reel-starts")
            {
                // Return our predetermined positions
                for (int i = 0; i < poolRequest.DrawCount; i++)
                {
                    results.Add((i < _reelPositions.Length ? _reelPositions[i] : 0).ToString());
                }
            }
            else
            {
                // For other pools, return zeros
                for (int i = 0; i < poolRequest.DrawCount; i++)
                {
                    results.Add("0");
                }
            }

            pools.Add(new RNGClient.PoolResult(poolRequest.PoolId, results, null));
        }

        return Task.FromResult(new RNGClient.PoolsResponse("OUTCOME-TEST", pools));
    }
}

// Helper classes (also used by Program.cs)
public sealed class SimulatorTimeService : ITimeService
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public long UnixMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class NullTelemetrySink : ISpinTelemetrySink
{
    public void Record(SpinTelemetryEvent @event) { }
}
