using System.Diagnostics;
using System.Text.Json;
using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;
using Simulator;

// Parse command line arguments
long spinCount = 1_000_000; // Default: 1M spins
decimal betAmount = 1.00m; // Default: $1.00 bet
string? outputFile = null;
int? seed = null;
bool verbose = false;
int progressInterval = 100_000;
bool interactiveMode = false;
bool simulationMode = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "-n" or "--spins":
            if (i + 1 < args.Length && long.TryParse(args[i + 1], out var n))
            {
                spinCount = n;
                simulationMode = true;
                i++;
            }
            break;
        case "-b" or "--bet":
            if (i + 1 < args.Length && decimal.TryParse(args[i + 1], out var b))
            {
                betAmount = b;
                i++;
            }
            break;
        case "-o" or "--output":
            if (i + 1 < args.Length)
            {
                outputFile = args[i + 1];
                i++;
            }
            break;
        case "-s" or "--seed":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var s))
            {
                seed = s;
                i++;
            }
            break;
        case "-v" or "--verbose":
            verbose = true;
            break;
        case "-p" or "--progress":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
            {
                progressInterval = p;
                i++;
            }
            break;
        case "-i" or "--interactive":
            interactiveMode = true;
            break;
        case "-h" or "--help":
            PrintHelp();
            return 0;
    }
}

// Initialize config path
var configPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "backend", "GameEngineHost", "configs"));
if (!Directory.Exists(configPath))
{
    configPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backend", "GameEngineHost", "configs"));
}
if (!Directory.Exists(configPath))
{
    configPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "backend", "GameEngineHost", "configs"));
}

if (!Directory.Exists(configPath))
{
    Console.WriteLine();
    Console.WriteLine($"ERROR: Configuration directory not found: {configPath}");
    Console.WriteLine("Please run the simulator from the StarBurst root directory.");
    return 1;
}

// If no specific mode requested, show main menu
if (!interactiveMode && !simulationMode)
{
    return await ShowMainMenu(configPath, seed, betAmount, outputFile, progressInterval);
}

// Direct mode execution
if (interactiveMode)
{
    var tester = new OutcomeTester(configPath);
    await tester.RunInteractiveMode();
    return 0;
}

// Simulation mode
return await RunSimulation(configPath, spinCount, betAmount, outputFile, seed, progressInterval);

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// Main Menu
// ═══════════════════════════════════════════════════════════════════════════════════════════════

static async Task<int> ShowMainMenu(string configPath, int? seed, decimal betAmount, string? outputFile, int progressInterval)
{
    while (true)
    {
        Console.Clear();
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    STARBURST HEADLESS MATH SIMULATOR                         ║");
        Console.WriteLine("║                       GLI-19 Compliance Testing                              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │                              MAIN MENU                                      │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("    1. Run Simulation (RTP/Hit Frequency Analysis)");
        Console.WriteLine("    2. Test Specific Outcome (Interactive Mode)");
        Console.WriteLine("    3. Quick Test (1,000 spins)");
        Console.WriteLine("    4. Standard Test (1,000,000 spins)");
        Console.WriteLine("    5. Extended Test (10,000,000 spins)");
        Console.WriteLine();
        Console.WriteLine("    0. Exit");
        Console.WriteLine();
        Console.Write("  Select option: ");

        var input = Console.ReadLine()?.Trim();

        switch (input)
        {
            case "1":
                Console.Write("\n  Enter number of spins: ");
                if (!long.TryParse(Console.ReadLine()?.Trim(), out var customSpins) || customSpins <= 0)
                {
                    Console.WriteLine("  Invalid input. Press Enter to continue...");
                    Console.ReadLine();
                    break;
                }
                Console.Write("  Enter bet amount (e.g., 1.00): R");
                if (!decimal.TryParse(Console.ReadLine()?.Trim(), out var customBet) || customBet <= 0)
                {
                    Console.WriteLine("  Invalid bet amount. Using default R1.00");
                    customBet = 1.00m;
                }
                return await RunSimulation(configPath, customSpins, customBet, outputFile, seed, progressInterval);

            case "2":
                var tester = new OutcomeTester(configPath);
                await tester.RunInteractiveMode();
                break;

            case "3":
                return await RunSimulation(configPath, 1_000, betAmount, outputFile, seed, progressInterval);

            case "4":
                return await RunSimulation(configPath, 1_000_000, betAmount, outputFile, seed, progressInterval);

            case "5":
                return await RunSimulation(configPath, 10_000_000, betAmount, outputFile, seed, progressInterval);

            case "0":
                Console.WriteLine("\n  Goodbye!\n");
                return 0;

            default:
                Console.WriteLine("\n  Invalid option. Press Enter to continue...");
                Console.ReadLine();
                break;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// Simulation Runner
// ═══════════════════════════════════════════════════════════════════════════════════════════════

static async Task<int> RunSimulation(string configPath, long spinCount, decimal betAmount, string? outputFile, int? seed, int progressInterval)
{
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    STARBURST HEADLESS MATH SIMULATOR                         ║");
    Console.WriteLine("║                       GLI-19 Compliance Testing                              ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
Console.WriteLine($"  Configuration:");
Console.WriteLine($"    Spins:           {spinCount:N0}");
Console.WriteLine($"    Bet Amount:      R{betAmount:F2}");
Console.WriteLine($"    Output File:     {outputFile ?? "(console only)"}");
Console.WriteLine($"    Seed:            {seed?.ToString() ?? "(random)"}");
Console.WriteLine($"    Parallelism:     {Environment.ProcessorCount} cores");
    Console.WriteLine();

    Console.WriteLine($"  Config Path:       {configPath}");

    // Pre-load configuration
    var configLoader = new GameConfigurationLoader(configPath);
    GameConfiguration gameConfig;
    try
    {
        gameConfig = await configLoader.GetConfigurationAsync("starburst");
        Console.WriteLine($"  Game Config:       {gameConfig.GameId} v{gameConfig.Version}");
        Console.WriteLine($"  Board:             {gameConfig.Board.Columns}x{gameConfig.Board.Rows}");
        Console.WriteLine($"  Symbols:           {gameConfig.SymbolCatalog.Count}");
        Console.WriteLine($"  Max Win:           {gameConfig.MaxWinMultiplier}x");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine($"ERROR: Failed to load game configuration: {ex.Message}");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("  Starting simulation...");
    Console.WriteLine();

    // Initialize statistics collector
    var stats = new StatisticsCollector();
    var stopwatch = Stopwatch.StartNew();
    var processedSpins = 0L;

    // Create thread-local factory for spin handlers
    var threadLocalData = new ThreadLocal<ThreadLocalSpinContext>(() =>
    {
        var localConfigLoader = new GameConfigurationLoader(configPath);
        var localPrng = new FortunaPrng();
        var localRngClient = new LocalRngClient(seed);
        var localTimeService = new SimulatorTimeService();
        var localTelemetry = new NullTelemetrySink();
        var localWinEvaluator = new WinEvaluator();

        var localSpinHandler = new SpinHandler(
            localConfigLoader,
            localWinEvaluator,
            localTimeService,
            localPrng,
            localRngClient,
            localTelemetry);

        return new ThreadLocalSpinContext(localSpinHandler, localPrng);
    }, trackAllValues: true);

    // Suppress console output from SpinHandler during simulation
    var originalOut = Console.Out;
    Console.SetOut(TextWriter.Null);

    try
    {
        // Run parallel simulation
        await Parallel.ForAsync(0L, spinCount, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, async (i, ct) =>
        {
            var context = threadLocalData.Value!;

            // Create play request
            var request = new PlayRequest(
                GameId: "starburst",
                PlayerToken: $"SIM-{i}",
                Bets: new[] { new BetRequest("main", new Money(betAmount)) },
                BaseBet: new Money(betAmount),
                TotalBet: new Money(betAmount),
                BetMode: BetMode.Standard,
                IsFeatureBuy: false,
                EngineState: null,
                UserPayload: null,
                LastResponse: null,
                Bet: new Money(betAmount),
                FunMode: false);

            // Execute spin
            var response = await context.SpinHandler.PlayAsync(request, ct);

            // Track wild reel count from feature outcome
            var wildReelCount = 0;
            if (response.Feature?.LockedReels != null)
            {
                wildReelCount = response.Feature.LockedReels.Count;
            }

            // Record statistics
            var spinResult = new SpinResult
            {
                BetAmount = betAmount,
                WinAmount = response.Win.Amount,
                WinMultiplier = betAmount > 0 ? response.Win.Amount / betAmount : 0,
                WinCapHit = response.Win.Amount >= betAmount * gameConfig.MaxWinMultiplier,
                WildReelCount = wildReelCount,
                IsRespin = response.NextState?.IsInRespinFeature ?? false,
                Wins = response.Results.Wins.Select(w => new WinDetail
                {
                    SymbolCode = w.SymbolCode,
                    MatchCount = w.Count,
                    PayoutAmount = w.Payout.Amount,
                    PaylineId = w.PaylineId
                }).ToList()
            };

            stats.RecordSpin(spinResult);

            // Handle respins if triggered
            if (response.NextState?.IsInRespinFeature == true)
            {
                var respinState = response.NextState;
                while (respinState?.IsInRespinFeature == true)
                {
                    // Respins use the original bet for win calculations but don't charge player again
                    var respinRequest = new PlayRequest(
                        GameId: "starburst",
                        PlayerToken: $"SIM-{i}-RESPIN",
                        Bets: new[] { new BetRequest("main", new Money(betAmount)) },
                        BaseBet: new Money(betAmount),
                        TotalBet: new Money(betAmount),
                        BetMode: BetMode.Standard,
                        IsFeatureBuy: false,
                        EngineState: respinState,
                        UserPayload: null,
                        LastResponse: null,
                        Bet: new Money(betAmount),
                        FunMode: false);

                    var respinResponse = await context.SpinHandler.PlayAsync(respinRequest, ct);

                    var respinWildCount = respinResponse.Feature?.LockedReels?.Count ?? 0;

                    var respinResult = new SpinResult
                    {
                        BetAmount = 0, // No bet for respins (don't count in total bet)
                        WinAmount = respinResponse.Win.Amount,
                        WinMultiplier = betAmount > 0 ? respinResponse.Win.Amount / betAmount : 0,
                        WinCapHit = respinResponse.Win.Amount >= betAmount * gameConfig.MaxWinMultiplier,
                        WildReelCount = respinWildCount,
                        IsRespin = true,
                        Wins = respinResponse.Results.Wins.Select(w => new WinDetail
                        {
                            SymbolCode = w.SymbolCode,
                            MatchCount = w.Count,
                            PayoutAmount = w.Payout.Amount,
                            PaylineId = w.PaylineId
                        }).ToList()
                    };

                    stats.RecordSpin(respinResult);
                    respinState = respinResponse.NextState;
                }
            }

            // Progress reporting
            var current = Interlocked.Increment(ref processedSpins);
            if (current % progressInterval == 0)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                var rate = current * 1000.0 / elapsed;
                var remaining = (spinCount - current) / rate;

                Console.SetOut(originalOut);
                Console.Write($"\r  Progress: {current:N0} / {spinCount:N0} ({current * 100.0 / spinCount:F1}%) - {rate:N0} spins/sec - ETA: {remaining:F0}s   ");
                Console.SetOut(TextWriter.Null);
            }
        });
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    stopwatch.Stop();

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"  Simulation complete! {spinCount:N0} spins in {stopwatch.Elapsed.TotalSeconds:F2}s");
    Console.WriteLine();

    // Generate report
    var reportGenerator = new ReportGenerator();
    var snapshot = stats.GetSnapshot();
    var report = reportGenerator.GenerateReport(snapshot, stopwatch.Elapsed);

    Console.WriteLine(report);

    // Write to file if specified
    if (!string.IsNullOrEmpty(outputFile))
    {
        reportGenerator.WriteToFile(outputFile, snapshot, stopwatch.Elapsed);
        Console.WriteLine($"Report saved to: {outputFile}");
    }

    // Cleanup
    threadLocalData.Dispose();

    Console.WriteLine("\n  Press Enter to continue...");
    Console.ReadLine();

    return 0;
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// Helper Methods
// ═══════════════════════════════════════════════════════════════════════════════════════════════

static void PrintHelp()
{
    Console.WriteLine("Starburst Headless Math Simulator");
    Console.WriteLine();
    Console.WriteLine("Usage: Simulator [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -n, --spins <count>     Number of spins to simulate (default: 1,000,000)");
    Console.WriteLine("  -b, --bet <amount>      Bet amount per spin (default: 1.00)");
    Console.WriteLine("  -o, --output <file>     Output file path for report");
    Console.WriteLine("  -s, --seed <seed>       Random seed for reproducibility");
    Console.WriteLine("  -p, --progress <n>      Progress update interval (default: 100,000)");
    Console.WriteLine("  -i, --interactive       Launch interactive outcome tester");
    Console.WriteLine("  -v, --verbose           Enable verbose output");
    Console.WriteLine("  -h, --help              Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  Simulator                       # Show main menu");
    Console.WriteLine("  Simulator -n 10000000           # Run 10M spins directly");
    Console.WriteLine("  Simulator -i                    # Launch interactive mode");
    Console.WriteLine("  Simulator -n 1000000 -o report.txt");
}

// Thread-local context for parallel execution
record ThreadLocalSpinContext(SpinHandler SpinHandler, FortunaPrng Prng);
