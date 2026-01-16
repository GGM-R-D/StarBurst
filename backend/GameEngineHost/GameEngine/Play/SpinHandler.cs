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
        
        // CRITICAL: Clear respin state if it's invalid or exhausted BEFORE determining spin mode
        // This prevents stale respin state from incorrectly setting spinMode to Respin
        if (nextState.Respins is not null)
        {
            // Clear if respins are exhausted (feature has ended)
            if (nextState.Respins.RespinsRemaining <= 0)
            {
                Console.WriteLine($"[SpinHandler] Clearing respin state (respins exhausted: {nextState.Respins.RespinsRemaining})");
                nextState.Respins = null;
            }
            // Clear if request.EngineState is null (completely new session)
            else if (request.EngineState is null)
            {
                Console.WriteLine($"[SpinHandler] Clearing respin state (new session, no previous state)");
                nextState.Respins = null;
            }
        }
        
        // Determine spin mode AFTER clearing invalid respin state
        // Note: No free spins feature in Starburst (only wild/respin feature)
        var spinMode = request.IsFeatureBuy
            ? SpinMode.BuyEntry
            : nextState.IsInRespinFeature ? SpinMode.Respin
            : SpinMode.BaseGame;
        
        // Additional safety: If we determined BaseGame but respin state still exists, clear it
        if (spinMode == SpinMode.BaseGame && nextState.Respins is not null)
        {
            Console.WriteLine($"[SpinHandler] Clearing stale respin state (determined BaseGame but respin state exists: {nextState.Respins.RespinsRemaining} respins)");
            nextState.Respins = null;
        }
        
        Console.WriteLine($"[SpinHandler] SpinMode: {spinMode}, RespinsRemaining: {nextState.Respins?.RespinsRemaining ?? 0}, LockedReels: {(nextState.Respins?.LockedWildReels != null && nextState.Respins.LockedWildReels.Count > 0 ? string.Join(",", nextState.Respins.LockedWildReels.Select(r => r + 1)) : "none")}, RequestHadRespinState: {request.EngineState?.Respins != null}");

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
            AssignMultiplierValue(symbol, configuration, request.BetMode, spinMode, null, randomContext));

        // During respin mode, preserve locked wild reels (don't spin them)
        // CRITICAL: Even if RespinsRemaining is 0, we should still preserve locked reels if they exist
        // This handles the case where the feature just ended but we need to preserve the locked reels for the final spin
        var lockedReelsForBoardCreation = (spinMode == SpinMode.Respin && nextState.Respins is not null && nextState.Respins.LockedWildReels.Count > 0)
            ? nextState.Respins.LockedWildReels
            : null;

        // Detailed logging for debugging locked reels issue
        Console.WriteLine($"[SpinHandler] Board creation - SpinMode: {spinMode}, HasRespins: {nextState.Respins != null}, RespinsRemaining: {nextState.Respins?.RespinsRemaining ?? 0}");
        if (nextState.Respins != null)
        {
            Console.WriteLine($"[SpinHandler] Respin state details - LockedWildReels count: {nextState.Respins.LockedWildReels.Count}, LockedWildReels: [{string.Join(", ", nextState.Respins.LockedWildReels)}] (0-based)");
            if (nextState.Respins.LockedWildReels.Count > 0)
            {
                Console.WriteLine($"[SpinHandler] LockedWildReels (1-based for display): [{string.Join(", ", nextState.Respins.LockedWildReels.Select(r => r + 1))}]");
            }
        }
        Console.WriteLine($"[SpinHandler] lockedReelsForBoardCreation: {(lockedReelsForBoardCreation != null ? $"Count={lockedReelsForBoardCreation.Count}, Reels=[{string.Join(", ", lockedReelsForBoardCreation)}]" : "null")}");

        var board = ReelBoard.Create(
            reelStrips,
            configuration.SymbolMap,
            configuration.Board.Rows,
            multiplierFactory,
            randomContext.ReelStartSeeds,
            _fortunaPrng,
            lockedReelsForBoardCreation);
        Console.WriteLine("[SpinHandler] Board created");
        
        // IMPORTANT: Capture the initial grid BEFORE wild expansion for visual animation
        // This allows the frontend to show the single wild falling, then animate expansion
        var symbolMapper = configuration.SymbolIdMapper;
        var initialGridCodes = board.FlattenCodes();
        var initialGrid = symbolMapper.CodesToIds(initialGridCodes);
        Console.WriteLine($"[SpinHandler] Captured initial grid (before wild expansion) for visual animation");
        
        // IMPORTANT: Detect wilds from INITIAL board state (before any expansions or cascades)
        // This ensures we only detect naturally occurring wilds, not ones we expanded
        var initialWildReels = DetectWildReels(board);
        if (initialWildReels.Count > 0)
        {
            Console.WriteLine($"[SpinHandler] Initial wild reels detected (before expansions): {string.Join(", ", initialWildReels.Select(r => r + 1))}");
        }
        else
        {
            Console.WriteLine($"[SpinHandler] No initial wild reels detected (before expansions)");
        }
        
        // Starburst: Wilds can appear on reels 2, 3, 4 (indices 1, 2, 3)
        // Multiple wild reels are allowed - each awards a respin (max 3 respins)
        // During respins, wild reels are locked and only non-locked reels re-spin
        
        // Handle wild expansion based on spin mode
        if (spinMode == SpinMode.Respin && nextState.Respins is not null && nextState.Respins.RespinsRemaining > 0)
        {
            // STEP 1: Locked wild reels were already preserved during board creation (they didn't spin)
            // Verify they are still wilds as a safety check
            if (nextState.Respins.LockedWildReels.Count > 0)
            {
                Console.WriteLine($"[SpinHandler] Respin mode - verifying locked reels: {string.Join(",", nextState.Respins.LockedWildReels.Select(r => r + 1))}");
                // Check board state before verification
                foreach (var lockedReel in nextState.Respins.LockedWildReels)
                {
                    var reelSymbols = board.GetReelSymbols(lockedReel);
                    Console.WriteLine($"[SpinHandler] Board reel {lockedReel + 1} BEFORE verification: [{string.Join(", ", reelSymbols)}]");
                }
                LockWildReelsForRespin(board, nextState.Respins.LockedWildReels, configuration, multiplierFactory);
                Console.WriteLine($"[SpinHandler] Verified {nextState.Respins.LockedWildReels.Count} locked wild reels during respin: {string.Join(",", nextState.Respins.LockedWildReels.Select(r => r + 1))}");
                // Check board state after verification
                foreach (var lockedReel in nextState.Respins.LockedWildReels)
                {
                    var reelSymbols = board.GetReelSymbols(lockedReel);
                    var allWilds = reelSymbols.All(s => s == "WILD");
                    Console.WriteLine($"[SpinHandler] Board reel {lockedReel + 1} AFTER verification: [{string.Join(", ", reelSymbols)}] - AllWilds: {allWilds}");
                }
            }
            else
            {
                Console.WriteLine($"[SpinHandler] WARNING: Respin mode but no locked reels! This should not happen.");
            }
            
            // STEP 2: Detect NEW wilds that appeared on non-locked reels during this respin
            // (The board was just created with only non-locked reels spinning, locked reels are already wilds)
            var wildReelsAfterSpin = DetectWildReels(board);
            var newWildReels = wildReelsAfterSpin
                .Where(r => !nextState.Respins.LockedWildReels.Contains(r) && r >= 1 && r <= 3)
                .ToList();
            
            // STEP 3: Expand new wilds immediately (BEFORE win evaluation so they can substitute)
            if (newWildReels.Count > 0)
            {
                Console.WriteLine($"[SpinHandler] New wild reels detected during respin: {string.Join(", ", newWildReels.Select(r => r + 1))}");
                var wildDef = configuration.SymbolCatalog.FirstOrDefault(s => s.Code == "WILD");
                if (wildDef != null)
                {
                    foreach (var reelIndex in newWildReels)
                    {
                        board.ExpandReelToWild(reelIndex, wildDef, configuration.Board.Rows, multiplierFactory);
                        Console.WriteLine($"[SpinHandler] Expanded new wilds on reel {reelIndex + 1} during respin (before win evaluation)");
                    }
                }
                else
                {
                    Console.WriteLine($"[SpinHandler] ERROR: WILD symbol not found in symbol catalog!");
                }
                
                // Award additional respins for new wilds (will be processed after win evaluation)
                HandleWildRespinFeature(newWildReels, nextState, spinMode);
            }
        }
        else if ((spinMode == SpinMode.BaseGame || spinMode == SpinMode.BuyEntry) && initialWildReels.Count > 0)
        {
            // Base game: expand naturally occurring wilds immediately (before win evaluation)
            // ONLY expand if we actually detected wilds in the initial board
            var wildDef = configuration.SymbolCatalog.FirstOrDefault(s => s.Code == "WILD");
            if (wildDef != null)
            {
                foreach (var reelIndex in initialWildReels)
                {
                    board.ExpandReelToWild(reelIndex, wildDef, configuration.Board.Rows, multiplierFactory);
                    Console.WriteLine($"[SpinHandler] Expanded initial wilds on reel {reelIndex + 1} (BaseGame mode)");
                }
            }
            else
            {
                Console.WriteLine($"[SpinHandler] ERROR: WILD symbol not found in symbol catalog!");
            }
        }
        else if (spinMode == SpinMode.BaseGame && nextState.Respins is not null)
        {
            // SAFETY: BaseGame mode but respin state exists - this shouldn't happen after our clearing logic
            Console.WriteLine($"[SpinHandler] WARNING: BaseGame mode but respin state exists! RespinsRemaining: {nextState.Respins.RespinsRemaining}, LockedReels: {string.Join(",", nextState.Respins.LockedWildReels.Select(r => r + 1))}");
            // Don't expand any wilds - this is a safety measure
        }

        // Starburst is NOT a cascading game - it's a simple payline game
        // Evaluate wins once, pay them, done. No symbol removal, no cascades.
        // Note: No scatter symbols or free spins feature in Starburst
        var cascades = new List<CascadeStep>();
        var wins = new List<SymbolWin>();
        Money totalWin = Money.Zero;
        Money scatterWin = Money.Zero; // Always zero - no scatter symbols
        Money featureWin = Money.Zero; // No free spins feature
        int freeSpinsAwarded = 0; // Always zero - no free spins

        // Single win evaluation (no cascades)
        // IMPORTANT: All wild expansions have already happened above, so board state is final
        var gridCodes = board.FlattenCodes();
        Console.WriteLine($"[SpinHandler] Evaluating wins (single evaluation - no cascades)");
        var evaluation = _winEvaluator.Evaluate(gridCodes, configuration, effectiveBet);
        Console.WriteLine($"[SpinHandler] Win evaluation complete: {evaluation.SymbolWins.Count} wins, TotalWin={evaluation.TotalWin.Amount}");

        wins.AddRange(evaluation.SymbolWins);
        var baseWin = evaluation.TotalWin;
        var finalWin = baseWin;
        decimal appliedMultiplier = 1m;

        // Apply multipliers if any (from multiplier symbols on the board)
        // Note: No free spins feature, so multipliers only apply in base game/buy entry
        var multiplierSum = board.SumMultipliers();
        if (spinMode == SpinMode.BaseGame || spinMode == SpinMode.BuyEntry)
        {
            if (multiplierSum > 0m && baseWin.Amount > 0)
            {
                appliedMultiplier = multiplierSum;
                finalWin = baseWin * multiplierSum;
            }
        }

        totalWin = finalWin;

        // Create a single "cascade" step for compatibility (even though there are no cascades)
        // IMPORTANT: Use initialGrid (before expansion) as GridBefore, and finalGrid (after expansion) as GridAfter
        // This allows the frontend to animate the wild expansion visually
        var finalGrid = symbolMapper.CodesToIds(gridCodes);
        cascades.Add(new CascadeStep(
            Index: 0,
            GridBefore: initialGrid, // Grid BEFORE wild expansion (shows single wild)
            GridAfter: finalGrid, // Grid AFTER wild expansion (shows expanded wilds)
            WinsAfterCascade: evaluation.SymbolWins,
            BaseWin: baseWin,
            AppliedMultiplier: appliedMultiplier,
            TotalWin: finalWin));
        
        Console.WriteLine($"[SpinHandler] Single evaluation complete: {wins.Count} wins, TotalWin={totalWin.Amount}");

        // After win evaluation: Handle wild/respin feature state management
        // Note: Wild expansion already happened BEFORE win evaluation above
        // Note: No scatter symbols or free spins in Starburst - only wild/respin feature
        if (spinMode == SpinMode.BaseGame || spinMode == SpinMode.BuyEntry)
        {
            // Base game: initialize respin feature if wilds were detected
            // IMPORTANT: The current base game spin is NOT a respin - it's the triggering spin
            // Respins will happen on subsequent requests
            if (initialWildReels.Count > 0)
            {
                HandleWildRespinFeature(initialWildReels, nextState, spinMode);
                // Don't change spinMode here - the current spin is still base game
                // The respin mode will be determined on the NEXT request based on nextState
                if (nextState.Respins is not null && nextState.Respins.RespinsRemaining > 0)
                {
                    Console.WriteLine($"[SpinHandler] Respin feature triggered: {nextState.Respins.RespinsRemaining} respins will be awarded on next request(s)");
                }
            }
        }
        else if (spinMode == SpinMode.Respin && nextState.Respins is not null)
        {
            // Respin mode: New wilds were already detected and expanded BEFORE win evaluation
            // Now we just need to manage the respin state
            
            var respinsBeforeDecrement = nextState.Respins.RespinsRemaining;
            
            // This respin is complete - decrement respin count
            nextState.Respins.RespinsRemaining = Math.Max(0, nextState.Respins.RespinsRemaining - 1);
            nextState.Respins.JustTriggered = false;
            
            Console.WriteLine($"[SpinHandler] Respin completed: RespinsRemaining {respinsBeforeDecrement} -> {nextState.Respins.RespinsRemaining}");
            
            // IMPORTANT: Keep Respins object with RespinsRemaining=0 so ResponseTransformer can detect feature ended
            // It will be cleared on the next base game spin (see clearing logic at start of method)
            if (nextState.Respins.RespinsRemaining == 0)
            {
                Console.WriteLine($"[SpinHandler] Respin feature ended - all respins exhausted. Keeping Respins object with RespinsRemaining=0 for one response to signal feature closure.");
            }
            else
            {
                Console.WriteLine($"[SpinHandler] Respin feature continues - {nextState.Respins.RespinsRemaining} respin(s) remaining");
            }
        }

        var maxWin = Money.FromBet(effectiveBet.Amount, configuration.MaxWinMultiplier);
        if (totalWin.Amount > maxWin.Amount)
        {
            totalWin = maxWin;
        }

        // No free spins feature in Starburst - featureSummary is always null
        FeatureSummary? featureSummary = null;

        // Final grid is already set from the single evaluation (no cascades in Starburst)
        // All wild expansions happened before win evaluation, so gridCodes already has final state
        // Just verify and log the final grid
            var finalGridCodes = board.FlattenCodes();
            finalGrid = symbolMapper.CodesToIds(finalGridCodes);
        
        // Log grid in readable format (5 columns x 3 rows) - show both readable and ID format
        var spinTypeLabel = spinMode == SpinMode.Respin ? "RESPIN" : "BASE";
        LogGridLayout(finalGrid, configuration, $"{spinTypeLabel} SPIN - RoundId: {roundId}", roundId, wins);
        
        Console.WriteLine($"[SpinHandler] Final grid ready: {finalGrid.Count} symbols");

        // Determine game mode: 0=normal, 2=bonus game (respin feature), 3=free bets
        // Note: No free spins (mode 1) in Starburst
        // Use request.Mode if provided, otherwise infer from engine state
        var gameMode = request.Mode ?? (nextState.IsInRespinFeature ? 2 : 0);
        
        // Determine feature outcome if feature is active
        // Note: Only respin feature exists in Starburst (no free spins)
        FeatureOutcome? featureOutcome = null;
        if (nextState.Respins is not null && nextState.Respins.RespinsRemaining > 0)
        {
            var isClosure = nextState.Respins.RespinsRemaining == 0 ? 1 : 0;
            featureOutcome = new FeatureOutcome(
                Type: "BONUS_GAME",
                IsClosure: isClosure,
                Name: "Starburst Wilds");
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
            ScatterWin: Money.Zero, // No scatter symbols in Starburst
            FeatureWin: featureSummary?.FeatureWin ?? Money.Zero,
            BuyCost: buyCost,
            FreeSpinsAwarded: freeSpinsAwarded,
            RoundId: roundId,
            Timestamp: _timeService.UtcNow,
            NextState: nextState,
            Results: new ResultsEnvelope(
                Cascades: cascades,
                Wins: wins,
                Scatter: null, // No scatter symbols in Starburst
                FreeSpins: null, // No free spins feature in Starburst
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
            ScatterWin: 0m, // No scatter symbols in Starburst
            FeatureWin: featureSummary?.FeatureWin.Amount ?? 0m,
            BuyCost: buyCost.Amount,
            Cascades: cascades.Count,
            TriggeredFreeSpins: false, // No free spins feature in Starburst
            FreeSpinMultiplier: 0m, // No free spins feature in Starburst
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
        // Note: No free spins feature in Starburst - only base game, buy entry, and respin
        return mode switch
        {
            SpinMode.BuyEntry => configuration.ReelLibrary.Buy,
            SpinMode.Respin => SelectBaseReels(configuration), // Use base reels for respins
            _ => SelectBaseReels(configuration) // Base game - always use standard bet mode
        };
    }

    private IReadOnlyList<IReadOnlyList<string>> SelectBaseReels(GameConfiguration configuration)
    {
        // Note: No ante bet mode in Starburst - only standard bet mode
        if (!configuration.BetModes.TryGetValue("standard", out var modeDefinition))
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
        // Note: No ante bet mode or free spins in Starburst - only standard bet mode
        if (definition.Type != SymbolType.Multiplier)
        {
            return 0m;
        }

        // Always use standard multiplier profile (no ante bet, no free spins)
        var profile = configuration.MultiplierProfiles.Standard;

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

    // Note: Scatter symbols and free spins feature removed - not part of Starburst game

    private string CreateRoundId()
    {
        var randomSuffix = _fortunaPrng.NextInt32(0, int.MaxValue);
        return $"{_timeService.UnixMilliseconds:X}-{randomSuffix:X}";
    }

    /// <summary>
    /// Detects wild reels on reels 2, 3, 4 (indices 1, 2, 3).
    /// Returns list of reel indices that contain wild symbols.
    /// Only detects reels that ACTUALLY have at least one wild symbol (not already expanded).
    /// </summary>
    private static List<int> DetectWildReels(ReelBoard board)
    {
        const string WILD_CODE = "WILD";
        var wildReels = new List<int>();
        
        // Check which reels (2, 3, 4) have wilds (0-based indices: 1, 2, 3)
        for (int reelIndex = 1; reelIndex <= 3 && reelIndex < board.ColumnCount; reelIndex++)
        {
            var reelSymbols = board.GetReelSymbols(reelIndex);
            var wildCount = reelSymbols.Count(s => s == WILD_CODE);
            
            // Only detect reels that have at least one wild symbol
            // Note: This will detect both single wilds and already-expanded full wild reels
            if (wildCount > 0)
            {
                wildReels.Add(reelIndex);
                Console.WriteLine($"[SpinHandler] Reel {reelIndex + 1} has {wildCount} wild symbol(s)");
            }
        }
        
        if (wildReels.Count > 0)
        {
            Console.WriteLine($"[SpinHandler] Wild reels detected: {string.Join(", ", wildReels.Select(r => r + 1))}");
        }
        else
        {
            Console.WriteLine($"[SpinHandler] No wild reels detected on reels 2, 3, 4");
        }
        
        return wildReels;
    }

    /// <summary>
    /// Handles Starburst Wild Respin feature:
    /// - Wilds on reels 2, 3, 4 expand and lock
    /// - Each wild reel awards one respin (max 3 respins total)
    /// - New wilds during respins also expand and award additional respins
    /// </summary>
    private static void HandleWildRespinFeature(List<int> wildReels, EngineSessionState state, SpinMode currentMode)
    {
        const int MAX_RESPINS = 3;
        
        if (state.Respins is null)
        {
            // Initialize respin feature
            // IMPORTANT: Each wild reel awards exactly 1 respin (max 3 total)
            var newLockedReels = new HashSet<int>(wildReels);
            var respinsAwarded = Math.Min(wildReels.Count, MAX_RESPINS);
            
            Console.WriteLine($"[SpinHandler] Initializing respin feature: {wildReels.Count} wild reel(s) detected on reels {string.Join(", ", wildReels.Select(r => r + 1))}, awarding {respinsAwarded} respin(s)");
            
            state.Respins = new RespinState
            {
                RespinsRemaining = respinsAwarded,
                LockedWildReels = newLockedReels,
                TotalRespinsAwarded = respinsAwarded,
                JustTriggered = true
            };
            
            Console.WriteLine($"[SpinHandler] Respin feature initialized: RespinsRemaining={respinsAwarded}, locked reels: {string.Join(", ", newLockedReels.Select(r => r + 1))}");
        }
        else
        {
            // Already in respin feature - check for new wild reels
            var existingLocked = state.Respins.LockedWildReels;
            var newWildReels = wildReels.Where(r => !existingLocked.Contains(r)).ToList();
            
            if (newWildReels.Count > 0)
            {
                // New wild reels found - add them to locked reels and award additional respins
                var totalLocked = existingLocked.Count + newWildReels.Count;
                var maxPossibleRespins = Math.Min(totalLocked, MAX_RESPINS);
                var currentRespins = state.Respins.RespinsRemaining;
                
                // Award additional respins, but don't exceed max of 3 total
                var additionalRespins = Math.Min(newWildReels.Count, maxPossibleRespins - currentRespins);
                if (additionalRespins > 0)
                {
                    state.Respins.RespinsRemaining = Math.Min(MAX_RESPINS, currentRespins + additionalRespins);
                    state.Respins.TotalRespinsAwarded += additionalRespins;
                    
                    // Add new reels to locked set
                    var updatedLocked = new HashSet<int>(existingLocked);
                    foreach (var reel in newWildReels)
                    {
                        updatedLocked.Add(reel);
                    }
                    state.Respins.LockedWildReels = updatedLocked;
                    
                    Console.WriteLine($"[SpinHandler] New wild reels during respin: {string.Join(", ", newWildReels.Select(r => r + 1))}, additional respins: {additionalRespins}, total locked: {updatedLocked.Count}");
                }
            }
        }
    }

    /// <summary>
    /// Locks wild reels during respin by expanding wilds to cover entire reel.
    /// Only non-locked reels will re-spin.
    /// Starburst rule: Wilds can ONLY appear on reels 2, 3, 4 (indices 1, 2, 3).
    /// </summary>
    private static void LockWildReelsForRespin(ReelBoard board, IReadOnlySet<int> lockedReels, GameConfiguration configuration, Func<SymbolDefinition, decimal> multiplierFactory)
    {
        const string WILD_CODE = "WILD";
        
        // SymbolMap is keyed by Sym (e.g., "Sym1"), not Code (e.g., "WILD")
        // So we need to search SymbolCatalog by Code instead
        var wildDef = configuration.SymbolCatalog.FirstOrDefault(s => s.Code == WILD_CODE);
        if (wildDef == null)
        {
            Console.WriteLine($"[SpinHandler] ERROR: WILD symbol not found in symbol catalog!");
            return;
        }
        
        foreach (var reelIndex in lockedReels)
        {
            // Starburst rule: Wilds can ONLY appear on reels 2, 3, 4 (indices 1, 2, 3)
            // Never expand wilds on reel 1 (index 0) or reel 5 (index 4)
            if (reelIndex < 1 || reelIndex > 3 || reelIndex >= board.ColumnCount)
            {
                Console.WriteLine($"[SpinHandler] WARNING: Attempted to lock wild on invalid reel {reelIndex + 1} (must be reels 2-4). Skipping.");
                continue;
            }
            
            // Log before expansion
            var reelSymbolsBefore = board.GetReelSymbols(reelIndex);
            Console.WriteLine($"[SpinHandler] Before locking reel {reelIndex + 1}: [{string.Join(", ", reelSymbolsBefore)}]");
            
            // Expand wilds to cover entire reel (all 3 rows)
            board.ExpandReelToWild(reelIndex, wildDef, configuration.Board.Rows, multiplierFactory);
            
            // Verify after expansion
            var reelSymbolsAfter = board.GetReelSymbols(reelIndex);
            var allWilds = reelSymbolsAfter.All(s => s == WILD_CODE);
            Console.WriteLine($"[SpinHandler] After locking reel {reelIndex + 1}: [{string.Join(", ", reelSymbolsAfter)}] - All wilds: {allWilds}");
            
            if (!allWilds)
            {
                Console.WriteLine($"[SpinHandler] ERROR: Reel {reelIndex + 1} was not fully expanded to wilds!");
            }
        }
    }

    private void LogGridLayout(IReadOnlyList<int> gridSymbolIds, GameConfiguration configuration, string label, string? roundId = null, IReadOnlyList<SymbolWin>? wins = null)
    {
        var cols = configuration.Board.Columns;
        var rows = configuration.Board.Rows;
        var symbolMapper = configuration.SymbolIdMapper;
        
        // Map symbol codes to readable names (matching frontend format)
        var symbolNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "WILD", "WILD" },
            { "BAR", "BAR" },
            { "SEVEN", "SEVEN" },
            { "RED", "RED" },
            { "PURPLE", "PURPLE" },
            { "BLUE", "BLUE" },
            { "GREEN", "GREEN" },
            { "ORANGE", "ORANGE" }
        };
        
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
        
        Console.WriteLine($"\n[SpinHandler] ========== {label} ==========");
        
        // Log wins with payline information
        if (wins != null && wins.Count > 0)
        {
            Console.WriteLine($"[SpinHandler] --- Wins ({wins.Count} total) ---");
            foreach (var win in wins)
            {
                var paylineInfo = "";
                if (win.PaylineId.HasValue && win.PaylineId.Value >= 1 && win.PaylineId.Value <= paylineDefinitions.Length)
                {
                    var paylineDef = paylineDefinitions[win.PaylineId.Value - 1];
                    paylineInfo = $"Payline {win.PaylineId.Value} [{string.Join(",", paylineDef)}]";
                }
                Console.WriteLine($"[SpinHandler]   {win.SymbolCode} x{win.Count} = {win.Payout.Amount:F2} (Multiplier: {win.Multiplier}x) - {paylineInfo}");
            }
        }
        
        // Grid is flattened as: [row2col0, row2col1, ..., row2col4, row1col0, ..., row0col0, ...]
        // Where row2 is bottom, row1 is middle, row0 is top
        // Display as rows from top to bottom (row0, row1, row2) - READABLE FORMAT
        Console.WriteLine($"[SpinHandler] --- Grid Layout (Readable Format) ---");
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
                        // Use readable name if available, otherwise use code
                        var symbolName = symbolNameMap.TryGetValue(symbolCode, out var name) ? name : symbolCode;
                        rowSymbols.Add($"{symbolName}({symbolId})");
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
                        // Use readable name if available, otherwise use code
                        var symbolName = symbolNameMap.TryGetValue(symbolCode, out var name) ? name : symbolCode;
                        colSymbols.Add($"{symbolName}({symbolId})");
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
        
        // Also show ID format for reference
        Console.WriteLine($"[SpinHandler] --- Grid Layout (ID Format) ---");
        for (int displayRow = rows - 1; displayRow >= 0; displayRow--)
        {
            var rowSymbols = new List<string>();
            for (int col = 0; col < cols; col++)
            {
                var flatIndex = displayRow * cols + col;
                if (flatIndex < gridSymbolIds.Count)
                {
                    var symbolId = gridSymbolIds[flatIndex];
                    rowSymbols.Add($"ID{symbolId}");
                }
                else
                {
                    rowSymbols.Add("EMPTY");
                }
            }
            var rowLabel = displayRow == rows - 1 ? "TOP" : displayRow == 0 ? "BOT" : "MID";
            Console.WriteLine($"[SpinHandler] {rowLabel} ROW: [{string.Join(" | ", rowSymbols)}]");
        }
        
        Console.WriteLine($"[SpinHandler] ============================================\n");
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
            FortunaPrng prng,
            IReadOnlySet<int>? lockedReels = null)
        {
            if (reelStrips.Count == 0)
            {
                throw new InvalidOperationException("Reel strips are not configured.");
            }

            // Get WILD symbol definition if we need to preserve locked reels
            // Note: SymbolMap is keyed by Sym (e.g., "Sym1"), not Code (e.g., "WILD")
            // So we need to search SymbolCatalog by Code instead
            SymbolDefinition? wildDef = null;
            if (lockedReels != null && lockedReels.Count > 0)
            {
                // Find WILD by searching SymbolCatalog (which has Code property)
                // We need to get it from the configuration, but we only have symbolMap here
                // So we'll search through all symbols in the map to find one with Code == "WILD"
                wildDef = symbolMap.Values.FirstOrDefault(s => s.Code == "WILD");
                if (wildDef == null)
                {
                    Console.WriteLine($"[ReelBoard] WARNING: Locked reels specified but WILD symbol not found in symbol catalog!");
                }
            }

            var columns = new List<ReelColumn>(reelStrips.Count);
            for (var columnIndex = 0; columnIndex < reelStrips.Count; columnIndex++)
            {
                var strip = reelStrips[columnIndex];
                if (strip.Count == 0)
                {
                    throw new InvalidOperationException($"Reel {columnIndex} is empty.");
                }

                // If this reel is locked, fill it with wilds instead of spinning
                var isLocked = lockedReels != null && lockedReels.Contains(columnIndex);
                var lockedReelsStr = lockedReels != null ? string.Join(",", lockedReels) : "null";
                Console.WriteLine($"[ReelBoard] Processing reel {columnIndex + 1} (index {columnIndex}): isLocked={isLocked}, hasWildDef={wildDef != null}, lockedReels={lockedReelsStr}");
                
                if (isLocked && wildDef != null)
                {
                    Console.WriteLine($"[ReelBoard] Creating locked reel {columnIndex + 1} (index {columnIndex}) as wilds");
                    // Create a column filled with wilds for locked reels
                    // CRITICAL: symbolMap is keyed by Sym (e.g., "Sym1"), not Code (e.g., "WILD")
                    // So we must use wildDef.Sym in the strip, not "WILD"
                    var wildSym = wildDef.Sym; // e.g., "Sym1"
                    var wildStrip = new List<string> { wildSym }; // Use Sym value, not Code
                    var wildColumn = new ReelColumn(wildStrip, 0, rows, symbolMap, multiplierFactory);
                    
                    // CRITICAL: Replace ALL symbols with wilds to ensure they're correct
                    // Even though the strip has the correct Sym, we explicitly set each symbol to be sure
                    for (int row = 0; row < rows; row++)
                    {
                        if (row < wildColumn.Symbols.Count)
                        {
                            var multiplier = multiplierFactory(wildDef);
                            wildColumn.Symbols[row] = new SymbolInstance(wildDef, multiplier);
                        }
                        else
                        {
                            // If somehow we don't have enough symbols, add them
                            var multiplier = multiplierFactory(wildDef);
                            wildColumn.Symbols.Add(new SymbolInstance(wildDef, multiplier));
                        }
                    }
                    
                    // Ensure we have exactly 'rows' symbols
                    while (wildColumn.Symbols.Count < rows)
                    {
                        var multiplier = multiplierFactory(wildDef);
                        wildColumn.Symbols.Add(new SymbolInstance(wildDef, multiplier));
                    }
                    
                    columns.Add(wildColumn);
                    
                    // Verify it worked
                    var verifySymbols = wildColumn.Symbols.Select(s => s.Definition.Code).ToList();
                    var allWilds = verifySymbols.All(s => s == "WILD");
                    var symbolCount = verifySymbols.Count;
                    Console.WriteLine($"[ReelBoard] Preserved locked reel {columnIndex + 1} as wilds (skipped spinning) - SymbolCount: {symbolCount}, ExpectedRows: {rows}, Symbols: [{string.Join(", ", verifySymbols)}], AllWilds: {allWilds}");
                    if (!allWilds || symbolCount != rows)
                    {
                        Console.WriteLine($"[ReelBoard] ERROR: Locked reel {columnIndex + 1} was not fully set to wilds! Expected {rows} wilds, got {symbolCount} symbols, allWilds={allWilds}");
                    }
                }
                else
                {
                    if (isLocked && wildDef == null)
                    {
                        Console.WriteLine($"[ReelBoard] WARNING: Reel {columnIndex + 1} is locked but wildDef is null! Spinning normally (this is wrong!)");
                    }
                    // Normal reel - spin it
                    var startIndex = reelStartSeeds is not null && columnIndex < reelStartSeeds.Count
                        ? Math.Abs(reelStartSeeds[columnIndex]) % strip.Count
                        : prng.NextInt32(0, strip.Count);
                    columns.Add(new ReelColumn(strip, startIndex, rows, symbolMap, multiplierFactory));
                }
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

        /// <summary>
        /// Removes symbols but preserves WILDs on locked reels during respins.
        /// Used to ensure locked wild reels stay as wilds during cascades.
        /// </summary>
        public void RemoveSymbolsExceptLockedWilds(ISet<string> targets, IReadOnlySet<int> lockedReels)
        {
            for (int reelIndex = 0; reelIndex < _columns.Count; reelIndex++)
            {
                var column = _columns[reelIndex];
                bool isLockedReel = lockedReels.Contains(reelIndex);
                
                if (isLockedReel && targets.Contains("WILD"))
                {
                    // On locked reels, only remove non-WILD symbols
                    // Preserve WILDs on locked reels
                    column.RemoveWhere(symbol => targets.Contains(symbol.Definition.Code) && symbol.Definition.Code != "WILD");
                }
                else
                {
                    // On non-locked reels, remove all target symbols (including WILDs)
                    column.RemoveWhere(symbol => targets.Contains(symbol.Definition.Code));
                }
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

        /// <summary>
        /// Expands a reel to be fully wild (used during respins to lock wild reels).
        /// Starburst rule: Wilds can ONLY appear on reels 2, 3, 4 (indices 1, 2, 3).
        /// </summary>
        public void ExpandReelToWild(int reelIndex, SymbolDefinition wildDef, int rows, Func<SymbolDefinition, decimal> multiplierFactory)
        {
            // Starburst rule: Wilds can ONLY appear on reels 2, 3, 4 (indices 1, 2, 3)
            // Never expand wilds on reel 1 (index 0) or reel 5 (index 4)
            if (reelIndex < 1 || reelIndex > 3 || reelIndex >= _columns.Count)
            {
                Console.WriteLine($"[ReelBoard] WARNING: Attempted to expand wild on invalid reel {reelIndex + 1} (must be reels 2-4). Skipping.");
                return;
            }

            var column = _columns[reelIndex];
            // Ensure we have enough symbols
            while (column.Symbols.Count < rows)
            {
                column.Refill(rows);
            }
            
            // Replace all symbols in this reel with wilds
            for (int row = 0; row < rows && row < column.Symbols.Count; row++)
            {
                var multiplier = multiplierFactory(wildDef);
                column.Symbols[row] = new SymbolInstance(wildDef, multiplier);
            }
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

