using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameEngine.Configuration;
using GameEngine.Services;
using Microsoft.Extensions.Logging;
using RNGClient;

namespace GameEngine.Play;

public sealed class SpinHandler
{
    private readonly GameConfigurationLoader _configurationLoader;
    private readonly WinEvaluator _winEvaluator;
    private readonly IRngClient _rngClient;
    private readonly ITimeService _timeService;
    private readonly FortunaPrng _fortunaPrng;
    private readonly ISpinTelemetrySink _telemetry;

    public SpinHandler(
        GameConfigurationLoader configurationLoader,
        WinEvaluator winEvaluator,
        ITimeService timeService,
        FortunaPrng fortunaPrng,
        IRngClient rngClient,
        ISpinTelemetrySink telemetry)
    {
        _configurationLoader = configurationLoader;
        _winEvaluator = winEvaluator;
        _timeService = timeService;
        _fortunaPrng = fortunaPrng;
        _rngClient = rngClient;
        _telemetry = telemetry;
    }

    public async Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SpinHandler] PlayAsync started: GameId={request.GameId}, TotalBet={request.TotalBet.Amount}");
        
        ValidateRequest(request);
        Console.WriteLine("[SpinHandler] Request validated");

        var configuration = await _configurationLoader.GetConfigurationAsync(request.GameId, cancellationToken);
        Console.WriteLine($"[SpinHandler] Configuration loaded for GameId={request.GameId}");
        
        var roundId = CreateRoundId();
        Console.WriteLine($"[SpinHandler] RoundId created: {roundId}");
        
        var nextState = request.EngineState?.Clone() ?? EngineSessionState.Create();
        var spinMode = request.IsFeatureBuy
            ? SpinMode.BuyEntry
            : nextState.IsInFreeSpins ? SpinMode.FreeSpins : SpinMode.BaseGame;
        Console.WriteLine($"[SpinHandler] SpinMode: {spinMode}");

        // Use bet field if provided (per RGS spec), otherwise use TotalBet
        // bet = calculated total bet as sum of all amounts in bets array
        var effectiveBet = request.Bet ?? request.TotalBet;

        var buyCost = request.IsFeatureBuy
            ? Money.FromBet(request.BaseBet.Amount, configuration.BuyFeature.CostMultiplier)
            : Money.Zero;

        var reelStrips = SelectReelStrips(configuration, spinMode, request.BetMode);
        Console.WriteLine($"[SpinHandler] Reel strips selected: {reelStrips.Count} reels");
        
        var randomContext = await FetchRandomContext(configuration, reelStrips, request, roundId, spinMode, cancellationToken);
        Console.WriteLine("[SpinHandler] Random context fetched");
        var multiplierFactory = new Func<SymbolDefinition, decimal>(symbol =>
            AssignMultiplierValue(symbol, configuration, request.BetMode, spinMode, nextState.FreeSpins, randomContext));

        var board = ReelBoard.Create(
            reelStrips,
            configuration.SymbolMap,
            configuration.Board.Rows,
            multiplierFactory,
            randomContext.ReelStartSeeds,
            _fortunaPrng);
        Console.WriteLine("[SpinHandler] Board created");
        
        // Starburst rule: Only ONE reel (2, 3, or 4) can have wilds per spin
        // If multiple reels have wilds, randomly select one and remove wilds from others
        EnforceSingleWildReel(board, configuration, multiplierFactory, _fortunaPrng);

        var cascades = new List<CascadeStep>();
        var wins = new List<SymbolWin>();
        var cascadeIndex = 0;
        Money totalWin = Money.Zero;
        Money scatterWin = Money.Zero;
        Money featureWin = nextState.FreeSpins?.FeatureWin ?? Money.Zero;
        int freeSpinsAwarded = 0;
        IReadOnlyList<int>? finalGrid = null;
        var symbolMapper = configuration.SymbolIdMapper;

        const int MAX_CASCADES = 50; // Safety limit to prevent infinite loops
        
        while (cascadeIndex < MAX_CASCADES)
        {
            var gridBeforeCodes = board.FlattenCodes();
            Console.WriteLine($"[SpinHandler] Evaluating wins (cascade {cascadeIndex})");
            var evaluation = _winEvaluator.Evaluate(gridBeforeCodes, configuration, effectiveBet);
            Console.WriteLine($"[SpinHandler] Win evaluation complete: {evaluation.SymbolWins.Count} wins, TotalWin={evaluation.TotalWin.Amount}");

            if (evaluation.SymbolWins.Count == 0)
            {
                Console.WriteLine($"[SpinHandler] No more wins, breaking cascade loop at cascade {cascadeIndex}");
                finalGrid = cascades.Count > 0 ? cascades[^1].GridAfter : symbolMapper.CodesToIds(gridBeforeCodes);
                break;
            }
            
            // Safety check: if we've hit max cascades, break
            if (cascadeIndex >= MAX_CASCADES - 1)
            {
                Console.WriteLine($"[SpinHandler] WARNING: Max cascades ({MAX_CASCADES}) reached, forcing break!");
                // Use current grid state after refill
                var finalGridCodes = board.FlattenCodes();
                finalGrid = symbolMapper.CodesToIds(finalGridCodes);
                Console.WriteLine($"[SpinHandler] Final grid set from board state: {finalGrid.Count} symbols");
                break;
            }

            wins.AddRange(evaluation.SymbolWins);
            var cascadeBaseWin = evaluation.TotalWin;
            var cascadeFinalWin = cascadeBaseWin;
            decimal appliedMultiplier = 1m;

            var multiplierSum = board.SumMultipliers();
            if (spinMode == SpinMode.BaseGame || spinMode == SpinMode.BuyEntry)
            {
                if (multiplierSum > 0m && cascadeBaseWin.Amount > 0)
                {
                    appliedMultiplier = multiplierSum;
                    cascadeFinalWin = cascadeBaseWin * multiplierSum;
                }
            }
            else if (nextState.FreeSpins is not null)
            {
                if (multiplierSum > 0m)
                {
                    nextState.FreeSpins.TotalMultiplier += multiplierSum;
                }

                if (nextState.FreeSpins.TotalMultiplier > 0m && cascadeBaseWin.Amount > 0)
                {
                    appliedMultiplier = nextState.FreeSpins.TotalMultiplier;
                    cascadeFinalWin = cascadeBaseWin * nextState.FreeSpins.TotalMultiplier;
                }
            }

            totalWin += cascadeFinalWin;

            if (spinMode == SpinMode.FreeSpins && nextState.FreeSpins is not null)
            {
                featureWin += cascadeFinalWin;
                nextState.FreeSpins.FeatureWin = featureWin;
            }

            var winningCodes = evaluation.SymbolWins
                .Select(win => win.SymbolCode)
                .ToHashSet(StringComparer.Ordinal);
            
            Console.WriteLine($"[SpinHandler] Removing {winningCodes.Count} winning symbol types: {string.Join(", ", winningCodes)}");
            var gridBeforeRemove = board.FlattenCodes();
            var symbolsBeforeCount = gridBeforeRemove.Count(s => winningCodes.Contains(s));
            
            board.RemoveSymbols(winningCodes);
            board.RemoveMultipliers();

            var gridAfterRemove = board.FlattenCodes();
            var symbolsAfterCount = gridAfterRemove.Count(s => winningCodes.Contains(s));
            Console.WriteLine($"[SpinHandler] Symbols removed: {symbolsBeforeCount} -> {symbolsAfterCount} remaining");

            if (board.NeedsRefill)
            {
                Console.WriteLine("[SpinHandler] Board needs refill, refilling...");
                board.Refill();
            }

            var gridAfterCodes = board.FlattenCodes();
            Console.WriteLine($"[SpinHandler] Cascade {cascadeIndex} complete, grid size: {gridAfterCodes.Count}");
            
            // Safety check: if grid hasn't changed, break to prevent infinite loop
            if (gridBeforeCodes.SequenceEqual(gridAfterCodes))
            {
                Console.WriteLine($"[SpinHandler] WARNING: Grid unchanged after cascade {cascadeIndex}, breaking to prevent infinite loop!");
                finalGrid = symbolMapper.CodesToIds(gridAfterCodes);
                break;
            }

            cascades.Add(new CascadeStep(
                Index: cascadeIndex++,
                GridBefore: symbolMapper.CodesToIds(gridBeforeCodes),
                GridAfter: symbolMapper.CodesToIds(gridAfterCodes),
                WinsAfterCascade: evaluation.SymbolWins,
                BaseWin: cascadeBaseWin,
                AppliedMultiplier: appliedMultiplier,
                TotalWin: cascadeFinalWin));
        }

        var scatterOutcome = ResolveScatterOutcome(board, configuration, effectiveBet);
        if (scatterOutcome is not null)
        {
            scatterWin = scatterOutcome.Win;
            totalWin += scatterWin;

            if ((spinMode == SpinMode.BaseGame || spinMode == SpinMode.BuyEntry) && scatterOutcome.FreeSpinsAwarded > 0)
            {
                InitializeFreeSpins(configuration, nextState);
                spinMode = SpinMode.FreeSpins;
                freeSpinsAwarded = scatterOutcome.FreeSpinsAwarded;
            }
            else if (spinMode == SpinMode.FreeSpins && nextState.FreeSpins is not null)
            {
                if (scatterOutcome.SymbolCount >= configuration.FreeSpins.RetriggerScatterCount)
                {
                    nextState.FreeSpins.SpinsRemaining += configuration.FreeSpins.RetriggerSpins;
                    nextState.FreeSpins.TotalSpinsAwarded += configuration.FreeSpins.RetriggerSpins;
                    freeSpinsAwarded = configuration.FreeSpins.RetriggerSpins;
                }
            }
        }

        if (spinMode == SpinMode.FreeSpins && nextState.FreeSpins is not null)
        {
            nextState.FreeSpins.SpinsRemaining = Math.Max(0, nextState.FreeSpins.SpinsRemaining - 1);
            nextState.FreeSpins.JustTriggered = false;

            if (nextState.FreeSpins.SpinsRemaining == 0)
            {
                nextState.FreeSpins = null;
            }
        }

        var maxWin = Money.FromBet(effectiveBet.Amount, configuration.MaxWinMultiplier);
        if (totalWin.Amount > maxWin.Amount)
        {
            totalWin = maxWin;
        }

        var featureSummary = nextState.FreeSpins is null
            ? null
            : new FeatureSummary(
                SpinsRemaining: nextState.FreeSpins.SpinsRemaining,
                TotalMultiplier: nextState.FreeSpins.TotalMultiplier,
                FeatureWin: nextState.FreeSpins.FeatureWin,
                TriggeredThisSpin: nextState.FreeSpins.JustTriggered);

        // Ensure finalGrid is set if it wasn't set during cascades
        if (finalGrid == null)
        {
            var finalGridCodes = board.FlattenCodes();
            finalGrid = symbolMapper.CodesToIds(finalGridCodes);
            Console.WriteLine($"[SpinHandler] Final grid set at end: {finalGrid.Count} symbols");
        }
        
        // Log grid in readable format (5 columns x 3 rows)
        LogGridLayout(finalGrid, configuration, "Final Grid");
        
        Console.WriteLine($"[SpinHandler] Final grid ready: {finalGrid.Count} symbols");

        // Determine game mode: 0=normal, 1=free spin, 2=bonus game, 3=free bets
        // Use request.Mode if provided, otherwise infer from engine state
        var gameMode = request.Mode ?? (nextState.IsInFreeSpins ? 1 : 0);
        
        // Determine feature outcome if feature is active
        FeatureOutcome? featureOutcome = null;
        if (nextState.FreeSpins is not null)
        {
            var isClosure = nextState.FreeSpins.SpinsRemaining == 0 ? 1 : 0;
            featureOutcome = new FeatureOutcome(
                Type: "FREESPINS",
                IsClosure: isClosure,
                Name: "Free Spins");
        }
        else if (request.IsFeatureBuy)
        {
            featureOutcome = new FeatureOutcome(
                Type: "BONUS_GAME",
                IsClosure: 0,
                Name: "Buy Feature");
        }

        Console.WriteLine($"[SpinHandler] Creating response: TotalWin={totalWin.Amount}, Wins={wins.Count}, Cascades={cascades.Count}");
        
        var response = new PlayResponse(
            StatusCode: 200,
            Win: totalWin,
            ScatterWin: scatterWin,
            FeatureWin: featureSummary?.FeatureWin ?? Money.Zero,
            BuyCost: buyCost,
            FreeSpinsAwarded: freeSpinsAwarded,
            RoundId: roundId,
            Timestamp: _timeService.UtcNow,
            NextState: nextState,
            Results: new ResultsEnvelope(
                Cascades: cascades,
                Wins: wins,
                Scatter: scatterOutcome,
                FreeSpins: featureSummary,
                RngTransactionId: roundId,
                FinalGridSymbols: finalGrid),
            Message: "Request processed successfully",
            Feature: featureOutcome);

        _telemetry.Record(new SpinTelemetryEvent(
            GameId: request.GameId,
            BetMode: request.BetMode,
            SpinMode: spinMode,
            TotalBet: request.TotalBet.Amount + buyCost.Amount,
            TotalWin: totalWin.Amount,
            ScatterWin: scatterWin.Amount,
            FeatureWin: featureSummary?.FeatureWin.Amount ?? 0m,
            BuyCost: buyCost.Amount,
            Cascades: cascades.Count,
            TriggeredFreeSpins: freeSpinsAwarded > 0,
            FreeSpinMultiplier: nextState.FreeSpins?.TotalMultiplier ?? 0m,
            Timestamp: response.Timestamp));

        Console.WriteLine($"[SpinHandler] PlayAsync completed successfully: RoundId={roundId}, Win={totalWin.Amount}");
        return response;
    }

    private static void ValidateRequest(PlayRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GameId))
        {
            throw new ArgumentException("gameId is required.", nameof(request.GameId));
        }

        if (request.Bets is null || request.Bets.Count == 0)
        {
            throw new ArgumentException("At least one bet entry is required.", nameof(request.Bets));
        }

        // EngineState can be null - it will be created if null (see line 44)
        // Validation removed to allow null engineState for first spin

        // Validate bet: use bet field if provided (per RGS spec), otherwise TotalBet
        var betToValidate = request.Bet ?? request.TotalBet;
        if (betToValidate.Amount <= 0)
        {
            throw new ArgumentException("Bet amount must be positive.", nameof(request.Bet));
        }
    }

    private IReadOnlyList<IReadOnlyList<string>> SelectReelStrips(
        GameConfiguration configuration,
        SpinMode mode,
        BetMode betMode)
    {
        return mode switch
        {
            SpinMode.FreeSpins => configuration.ReelLibrary.FreeSpins,
            SpinMode.BuyEntry => configuration.ReelLibrary.Buy,
            _ => SelectBaseReels(configuration, betMode)
        };
    }

    private IReadOnlyList<IReadOnlyList<string>> SelectBaseReels(GameConfiguration configuration, BetMode betMode)
    {
        var key = betMode == BetMode.Ante ? "ante" : "standard";
        if (!configuration.BetModes.TryGetValue(key, out var modeDefinition))
        {
            return configuration.ReelLibrary.High;
        }

        var lowWeight = Math.Max(0, modeDefinition.ReelWeights.Low);
        var highWeight = Math.Max(0, modeDefinition.ReelWeights.High);
        var total = lowWeight + highWeight;
        if (total <= 0)
        {
            return configuration.ReelLibrary.High;
        }

        var roll = _fortunaPrng.NextInt32(0, total);
        return roll < lowWeight ? configuration.ReelLibrary.Low : configuration.ReelLibrary.High;
    }

    private async Task<RandomContext> FetchRandomContext(
        GameConfiguration configuration,
        IReadOnlyList<IReadOnlyList<string>> reelStrips,
        PlayRequest request,
        string roundId,
        SpinMode spinMode,
        CancellationToken cancellationToken)
    {
        try
        {
            var pools = new List<PoolRequest>
            {
                new(
                    PoolId: "reel-starts",
                    DrawCount: reelStrips.Count,
                    Metadata: new Dictionary<string, object>
                    {
                        ["reelLengths"] = reelStrips.Select(strip => strip.Count).ToArray()
                    }),
                new(
                    PoolId: "multiplier-seeds",
                    DrawCount: configuration.Board.Columns * configuration.Board.Rows,
                    Metadata: new Dictionary<string, object>
                    {
                        ["multiplierValues"] = configuration.Multiplier.Values
                    })
            };

            var rngRequest = new JurisdictionPoolsRequest
            {
                GameId = request.GameId,
                RoundId = roundId,
                Pools = pools,
                TrackingData = new Dictionary<string, string>
                {
                    ["playerToken"] = request.PlayerToken,
                    ["mode"] = spinMode.ToString(),
                    ["betMode"] = request.BetMode.ToString()
                }
            };

            var response = await _rngClient.RequestPoolsAsync(rngRequest, cancellationToken).ConfigureAwait(false);
            var reelStartSeeds = ExtractIntegers(response, "reel-starts", reelStrips.Count);
            var multiplierSeeds = ExtractIntegers(response, "multiplier-seeds", configuration.Board.Columns * configuration.Board.Rows);

            return RandomContext.FromSeeds(reelStartSeeds, multiplierSeeds);
        }
        catch
        {
            return RandomContext.CreateFallback(reelStrips.Count, configuration.Board.Columns * configuration.Board.Rows, _fortunaPrng);
        }
    }

    private static IReadOnlyList<int> ExtractIntegers(PoolsResponse response, string poolId, int expectedCount)
    {
        var pool = response.Pools.FirstOrDefault(p => string.Equals(p.PoolId, poolId, StringComparison.OrdinalIgnoreCase));
        if (pool == null)
        {
            return Enumerable.Repeat(0, expectedCount).ToArray();
        }

        var results = new List<int>(expectedCount);
        foreach (var result in pool.Results.Take(expectedCount))
        {
            if (int.TryParse(result, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                results.Add(value);
            }
        }

        while (results.Count < expectedCount)
        {
            results.Add(results.Count);
        }

        return results;
    }

    private decimal AssignMultiplierValue(
        SymbolDefinition definition,
        GameConfiguration configuration,
        BetMode betMode,
        SpinMode spinMode,
        FreeSpinState? freeSpinState,
        RandomContext randomContext)
    {
        if (definition.Type != SymbolType.Multiplier)
        {
            return 0m;
        }

        IReadOnlyList<MultiplierWeight> profile = configuration.MultiplierProfiles.Standard;

        if (spinMode == SpinMode.FreeSpins && freeSpinState is not null)
        {
            profile = freeSpinState.TotalMultiplier >= configuration.MultiplierProfiles.FreeSpinsSwitchThreshold
                ? configuration.MultiplierProfiles.FreeSpinsLow
                : configuration.MultiplierProfiles.FreeSpinsHigh;
        }
        else if (betMode == BetMode.Ante)
        {
            profile = configuration.MultiplierProfiles.Ante;
        }

        var seed = randomContext.TryDequeueMultiplierSeed(out var rngSeed)
            ? rngSeed
            : _fortunaPrng.NextInt32(0, int.MaxValue);

        return RollMultiplier(profile, seed);
    }

    private decimal RollMultiplier(IReadOnlyList<MultiplierWeight> weights, int seed)
    {
        var total = weights.Sum(w => Math.Max(0, w.Weight));
        if (total <= 0)
        {
            return weights.Count > 0 ? weights[^1].Value : 0m;
        }

        var roll = Math.Abs(seed) % total;
        var cumulative = 0;
        foreach (var weight in weights)
        {
            cumulative += Math.Max(0, weight.Weight);
            if (roll < cumulative)
            {
                return weight.Value;
            }
        }

        return weights[^1].Value;
    }

    private static ScatterOutcome? ResolveScatterOutcome(ReelBoard board, GameConfiguration configuration, Money bet)
    {
        var scatterCount = board.CountSymbols(symbol => symbol.Type == SymbolType.Scatter);
        if (scatterCount == 0)
        {
            return null;
        }

        var reward = configuration.Scatter.Rewards
            .Where(r => scatterCount >= r.Count)
            .OrderByDescending(r => r.Count)
            .FirstOrDefault();

        if (reward is null)
        {
            return null;
        }

        var win = Money.FromBet(bet.Amount, reward.PayoutMultiplier);
        return new ScatterOutcome(scatterCount, win, reward.FreeSpinsAwarded);
    }

    private static void InitializeFreeSpins(GameConfiguration configuration, EngineSessionState state)
    {
        state.FreeSpins = new FreeSpinState
        {
            SpinsRemaining = configuration.FreeSpins.InitialSpins,
            TotalSpinsAwarded = configuration.FreeSpins.InitialSpins,
            TotalMultiplier = 0,
            FeatureWin = Money.Zero,
            JustTriggered = true
        };
    }

    private string CreateRoundId()
    {
        var randomSuffix = _fortunaPrng.NextInt32(0, int.MaxValue);
        return $"{_timeService.UnixMilliseconds:X}-{randomSuffix:X}";
    }

    /// <summary>
    /// Enforces Starburst rule: Only ONE reel (2, 3, or 4) can have wilds per spin.
    /// If multiple reels have wilds, randomly selects one and removes wilds from others.
    /// </summary>
    private static void EnforceSingleWildReel(ReelBoard board, GameConfiguration configuration, Func<SymbolDefinition, decimal> multiplierFactory, FortunaPrng prng)
    {
        const string WILD_CODE = "WILD";
        var wildReels = new List<int>(); // Reels that have wilds (0-based: 1, 2, 3 = reels 2, 3, 4)
        
        // Check which reels (2, 3, 4) have wilds
        for (int reelIndex = 1; reelIndex <= 3 && reelIndex < board.ColumnCount; reelIndex++)
        {
            var reelSymbols = board.GetReelSymbols(reelIndex);
            if (reelSymbols.Any(s => s == WILD_CODE))
            {
                wildReels.Add(reelIndex);
            }
        }
        
        // If multiple reels have wilds, randomly select one and remove wilds from others
        if (wildReels.Count > 1)
        {
            // Randomly select which reel keeps the wild
            var selectedReel = wildReels[prng.NextInt32(0, wildReels.Count)];
            Console.WriteLine($"[SpinHandler] Multiple wild reels detected: {string.Join(", ", wildReels.Select(r => r + 1))}. Selecting reel {selectedReel + 1} to keep wild.");
            
            // Remove wilds from other reels
            foreach (var reelIndex in wildReels)
            {
                if (reelIndex != selectedReel)
                {
                    board.ReplaceWildsWithRandomSymbol(reelIndex, WILD_CODE, configuration, prng, multiplierFactory);
                    Console.WriteLine($"[SpinHandler] Removed wilds from reel {reelIndex + 1}");
                }
            }
        }
        else if (wildReels.Count == 1)
        {
            Console.WriteLine($"[SpinHandler] Single wild reel detected: reel {wildReels[0] + 1}");
        }
    }

    private void LogGridLayout(IReadOnlyList<int> gridSymbolIds, GameConfiguration configuration, string label)
    {
        var cols = configuration.Board.Columns;
        var rows = configuration.Board.Rows;
        var symbolMapper = configuration.SymbolIdMapper;
        
        Console.WriteLine($"[SpinHandler] ========== {label} ({cols}x{rows}) ==========");
        
        // Grid is flattened as: [row2col0, row2col1, ..., row2col4, row1col0, ..., row0col0, ...]
        // Where row2 is bottom, row1 is middle, row0 is top
        // Display as rows from top to bottom (row0, row1, row2)
        for (int displayRow = rows - 1; displayRow >= 0; displayRow--) // Start from top (row 2 in 3-row grid)
        {
            var rowSymbols = new List<string>();
            for (int col = 0; col < cols; col++)
            {
                var flatIndex = displayRow * cols + col;
                if (flatIndex < gridSymbolIds.Count)
                {
                    var symbolId = gridSymbolIds[flatIndex];
                    try
                    {
                        var symbolCode = symbolMapper.IdToCode(symbolId);
                        rowSymbols.Add($"{symbolCode}({symbolId})");
                    }
                    catch
                    {
                        rowSymbols.Add($"ID{symbolId}");
                    }
                }
                else
                {
                    rowSymbols.Add("EMPTY");
                }
            }
            var rowLabel = displayRow == rows - 1 ? "TOP" : displayRow == 0 ? "BOT" : "MID";
            Console.WriteLine($"[SpinHandler] {rowLabel} ROW: [{string.Join(" | ", rowSymbols)}]");
        }
        
        // Also display as columns (reels) for easier comparison with frontend
        Console.WriteLine($"[SpinHandler] --- Column View (Reels) ---");
        for (int col = 0; col < cols; col++)
        {
            var colSymbols = new List<string>();
            for (int displayRow = rows - 1; displayRow >= 0; displayRow--) // Top to bottom
            {
                var flatIndex = displayRow * cols + col;
                if (flatIndex < gridSymbolIds.Count)
                {
                    var symbolId = gridSymbolIds[flatIndex];
                    try
                    {
                        var symbolCode = symbolMapper.IdToCode(symbolId);
                        colSymbols.Add($"{symbolCode}({symbolId})");
                    }
                    catch
                    {
                        colSymbols.Add($"ID{symbolId}");
                    }
                }
                else
                {
                    colSymbols.Add("EMPTY");
                }
            }
            Console.WriteLine($"[SpinHandler] REEL {col + 1}: [{string.Join(" | ", colSymbols)}]");
        }
        
        Console.WriteLine($"[SpinHandler] ============================================");
    }

    private sealed class ReelBoard
    {
        private readonly List<ReelColumn> _columns;
        private readonly int _rows;

        private ReelBoard(List<ReelColumn> columns, int rows)
        {
            _columns = columns;
            _rows = rows;
        }

        public static ReelBoard Create(
            IReadOnlyList<IReadOnlyList<string>> reelStrips,
            IReadOnlyDictionary<string, SymbolDefinition> symbolMap,
            int rows,
            Func<SymbolDefinition, decimal> multiplierFactory,
            IReadOnlyList<int> reelStartSeeds,
            FortunaPrng prng)
        {
            if (reelStrips.Count == 0)
            {
                throw new InvalidOperationException("Reel strips are not configured.");
            }

            var columns = new List<ReelColumn>(reelStrips.Count);
            for (var columnIndex = 0; columnIndex < reelStrips.Count; columnIndex++)
            {
                var strip = reelStrips[columnIndex];
                if (strip.Count == 0)
                {
                    throw new InvalidOperationException($"Reel {columnIndex} is empty.");
                }

                var startIndex = reelStartSeeds is not null && columnIndex < reelStartSeeds.Count
                    ? Math.Abs(reelStartSeeds[columnIndex]) % strip.Count
                    : prng.NextInt32(0, strip.Count);
                columns.Add(new ReelColumn(strip, startIndex, rows, symbolMap, multiplierFactory));
            }

            return new ReelBoard(columns, rows);
        }

        public bool NeedsRefill => _columns.Any(column => column.Count < _rows);

        public void Refill()
        {
            foreach (var column in _columns)
            {
                column.Refill(_rows);
            }
        }

        public List<string> FlattenCodes()
        {
            var snapshot = new List<string>(_columns.Count * _rows);

            for (var row = _rows - 1; row >= 0; row--)
            {
                foreach (var column in _columns)
                {
                    snapshot.Add(row < column.Count ? column[row].Definition.Code : string.Empty);
                }
            }

            return snapshot;
        }

        public List<int> FlattenIds(SymbolIdMapper mapper)
        {
            var snapshot = new List<int>(_columns.Count * _rows);

            for (var row = _rows - 1; row >= 0; row--)
            {
                foreach (var column in _columns)
                {
                    if (row < column.Count)
                    {
                        snapshot.Add(mapper.CodeToId(column[row].Definition.Code));
                    }
                    else
                    {
                        // Empty cell - use -1 or 0? For now, use 0 (first symbol) as placeholder
                        // This shouldn't happen in normal gameplay, but handle gracefully
                        snapshot.Add(0);
                    }
                }
            }

            return snapshot;
        }

        public decimal SumMultipliers() =>
            _columns.SelectMany(column => column.Symbols)
                .Where(instance => instance.Definition.Type == SymbolType.Multiplier)
                .Sum(instance => instance.MultiplierValue);

        public int CountSymbols(Func<SymbolDefinition, bool> predicate) =>
            _columns.SelectMany(column => column.Symbols)
                .Count(instance => predicate(instance.Definition));

        public void RemoveSymbols(ISet<string> targets)
        {
            foreach (var column in _columns)
            {
                column.RemoveWhere(symbol => targets.Contains(symbol.Definition.Code));
            }
        }

        public void RemoveMultipliers()
        {
            foreach (var column in _columns)
            {
                column.RemoveWhere(symbol => symbol.Definition.Type == SymbolType.Multiplier);
            }
        }

        public int ColumnCount => _columns.Count;

        public IReadOnlyList<string> GetReelSymbols(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= _columns.Count)
            {
                return Array.Empty<string>();
            }

            var column = _columns[reelIndex];
            var symbols = new List<string>(column.Count);
            for (int row = 0; row < column.Count; row++)
            {
                symbols.Add(column[row].Definition.Code);
            }
            return symbols;
        }

        public void ReplaceWildsWithRandomSymbol(int reelIndex, string wildCode, GameConfiguration configuration, FortunaPrng prng, Func<SymbolDefinition, decimal> multiplierFactory)
        {
            if (reelIndex < 0 || reelIndex >= _columns.Count)
            {
                return;
            }

            var column = _columns[reelIndex];
            var nonWildSymbols = configuration.SymbolCatalog
                .Where(s => s.Code != wildCode)
                .Select(s => s.Code)
                .ToList();

            if (nonWildSymbols.Count == 0)
            {
                return; // No replacement symbols available
            }

            for (int i = 0; i < column.Symbols.Count; i++)
            {
                if (column.Symbols[i].Definition.Code == wildCode)
                {
                    // Replace with random non-wild symbol
                    var randomSymbolCode = nonWildSymbols[prng.NextInt32(0, nonWildSymbols.Count)];
                    if (configuration.SymbolMap.TryGetValue(randomSymbolCode, out var newDefinition))
                    {
                        // Create new symbol instance with multiplier from factory
                        var multiplier = multiplierFactory(newDefinition);
                        column.Symbols[i] = new SymbolInstance(newDefinition, multiplier);
                    }
                }
            }
        }
    }

    private sealed class ReelColumn
    {
        private readonly IReadOnlyList<string> _strip;
        private readonly IReadOnlyDictionary<string, SymbolDefinition> _symbolMap;
        private readonly Func<SymbolDefinition, decimal> _multiplierFactory;
        private int _nextIndex;

        public List<SymbolInstance> Symbols { get; }

        public ReelColumn(
            IReadOnlyList<string> strip,
            int startIndex,
            int rows,
            IReadOnlyDictionary<string, SymbolDefinition> symbolMap,
            Func<SymbolDefinition, decimal> multiplierFactory)
        {
            _strip = strip;
            _symbolMap = symbolMap;
            _multiplierFactory = multiplierFactory;
            _nextIndex = startIndex;
            Symbols = new List<SymbolInstance>(rows);

            for (var i = 0; i < rows; i++)
            {
                Symbols.Add(CreateInstance());
            }
        }

        public int Count => Symbols.Count;

        public SymbolInstance this[int index] => Symbols[index];

        public void Refill(int desiredRows)
        {
            while (Symbols.Count < desiredRows)
            {
                Symbols.Add(CreateInstance());
            }
        }

        public void RemoveWhere(Func<SymbolInstance, bool> predicate) =>
            Symbols.RemoveAll(instance => predicate(instance));

        private SymbolInstance CreateInstance()
        {
            var definition = ResolveSymbol(_strip[_nextIndex]);
            _nextIndex = (_nextIndex + 1) % _strip.Count;
            var multiplier = _multiplierFactory(definition);
            return new SymbolInstance(definition, multiplier);
        }

        private SymbolDefinition ResolveSymbol(string symCode)
        {
            if (!_symbolMap.TryGetValue(symCode, out var definition))
            {
                throw new InvalidOperationException($"Unknown symbol `{symCode}` on reel.");
            }

            return definition;
        }
    }

    private sealed record SymbolInstance(SymbolDefinition Definition, decimal MultiplierValue);

    private sealed class RandomContext
    {
        private readonly IReadOnlyList<int> _reelSeeds;
        private readonly Queue<int> _multiplierSeeds;

        private RandomContext(IReadOnlyList<int> reelSeeds, Queue<int> multiplierSeeds)
        {
            _reelSeeds = reelSeeds;
            _multiplierSeeds = multiplierSeeds;
        }

        public IReadOnlyList<int> ReelStartSeeds => _reelSeeds;

        public bool TryDequeueMultiplierSeed(out int seed)
        {
            if (_multiplierSeeds.Count > 0)
            {
                seed = _multiplierSeeds.Dequeue();
                return true;
            }

            seed = 0;
            return false;
        }

        public static RandomContext FromSeeds(IReadOnlyList<int> reelSeeds, IReadOnlyList<int> multiplierSeeds) =>
            new(reelSeeds, new Queue<int>(multiplierSeeds));

        public static RandomContext CreateFallback(int reelCount, int multiplierSeedCount, FortunaPrng prng)
        {
            var reelSeeds = Enumerable.Range(0, reelCount)
                .Select(_ => prng.NextInt32(0, int.MaxValue))
                .ToArray();
            var multiplierSeeds = Enumerable.Range(0, multiplierSeedCount)
                .Select(_ => prng.NextInt32(0, int.MaxValue))
                .ToArray();
            return FromSeeds(reelSeeds, multiplierSeeds);
        }
    }
}

