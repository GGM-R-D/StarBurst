using System.Collections.Generic;
using System.Linq;
using GameEngine.Configuration;

namespace GameEngine.Play;

public sealed class WinEvaluator
{
    // Starburst 10-line payline definitions
    // Row indices: 0 = top, 1 = middle, 2 = bottom
    private static readonly IReadOnlyList<IReadOnlyList<int>> Paylines = new[]
    {
        new[] { 1, 1, 1, 1, 1 }, // Payline 1: Middle row (straight)
        new[] { 0, 0, 0, 0, 0 }, // Payline 2: Top row (straight)
        new[] { 2, 2, 2, 2, 2 }, // Payline 3: Bottom row (straight)
        new[] { 0, 1, 2, 1, 0 }, // Payline 4: V-shape up
        new[] { 2, 1, 0, 1, 2 }, // Payline 5: V-shape down (inverted V)
        new[] { 0, 0, 1, 0, 0 }, // Payline 6: Top-center (top-top-middle-top-top)
        new[] { 2, 2, 1, 2, 2 }, // Payline 7: Bottom-center (bottom-bottom-middle-bottom-bottom)
        new[] { 1, 2, 2, 2, 1 }, // Payline 8: Bottom-heavy (middle-bottom-bottom-bottom-middle)
        new[] { 1, 0, 0, 0, 1 }, // Payline 9: Top-heavy (middle-top-top-top-middle)
        new[] { 1, 0, 1, 0, 1 }  // Payline 10: Alternating (middle-top-middle-top-middle)
    };

    public WinEvaluationResult Evaluate(IReadOnlyList<string> grid, GameConfiguration configuration, Money bet)
    {
        var wins = new List<SymbolWin>();
        var totalWin = 0m;
        var columns = configuration.Board.Columns;
        var rows = configuration.Board.Rows;
        var betPerLine = bet.Amount / Paylines.Count; // Divide bet by number of paylines

        // Track wins per payline to ensure we only pay the highest win per payline
        var paylineWins = new Dictionary<int, SymbolWin>();

        for (int paylineIndex = 0; paylineIndex < Paylines.Count; paylineIndex++)
        {
            var payline = Paylines[paylineIndex];
            
            // Extract symbols along this payline
            var paylineSymbols = new List<string>();
            var paylinePositions = new List<int>();

            for (var reel = 0; reel < payline.Count && reel < columns; reel++)
            {
                var row = payline[reel];
                if (row < 0 || row >= rows) continue;

                // Grid is flattened as: [row2reel0, row2reel1, ..., row1reel0, ..., row0reel0, ...]
                // From FlattenCodes: for (var row = _rows - 1; row >= 0; row--) - bottom to top
                // So grid[0] = bottom-left, grid[columns] = bottom of reel 1, etc.
                // For row r and reel c: index = (rows - 1 - r) * columns + c
                var gridIndex = (rows - 1 - row) * columns + reel;
                if (gridIndex < grid.Count)
                {
                    paylineSymbols.Add(grid[gridIndex]);
                    paylinePositions.Add(gridIndex);
                }
            }

            if (paylineSymbols.Count < 3) continue; // Need at least 3 symbols for a win

            // Evaluate left-to-right
            var leftToRightWin = EvaluateDirection(paylineSymbols, paylinePositions, configuration, betPerLine, true);
            // Evaluate right-to-left
            var rightToLeftWin = EvaluateDirection(paylineSymbols, paylinePositions, configuration, betPerLine, false);

            // Choose the best win from both directions
            SymbolWin? bestWin = null;
            if (leftToRightWin != null && rightToLeftWin != null)
            {
                // Both directions have wins - choose the better one
                if (leftToRightWin.Count > rightToLeftWin.Count)
                {
                    bestWin = leftToRightWin;
                }
                else if (rightToLeftWin.Count > leftToRightWin.Count)
                {
                    bestWin = rightToLeftWin;
                }
                else
                {
                    // Same count - choose the one with higher payout
                    bestWin = leftToRightWin.Payout.Amount >= rightToLeftWin.Payout.Amount ? leftToRightWin : rightToLeftWin;
                }
            }
            else if (leftToRightWin != null)
            {
                bestWin = leftToRightWin;
            }
            else if (rightToLeftWin != null)
            {
                bestWin = rightToLeftWin;
            }

            // Store the best win for this payline (only highest win per payline is paid)
            if (bestWin != null)
            {
                var paylineId = paylineIndex + 1; // Payline IDs start at 1
                if (!paylineWins.ContainsKey(paylineId) || bestWin.Payout.Amount > paylineWins[paylineId].Payout.Amount)
                {
                    paylineWins[paylineId] = bestWin;
                }
            }
        }

        // Add all payline wins to the result
        wins.AddRange(paylineWins.Values);
        totalWin = wins.Sum(w => w.Payout.Amount);

        return new WinEvaluationResult(new Money(totalWin), wins);
    }

    private static SymbolWin? EvaluateDirection(
        IReadOnlyList<string> symbols,
        IReadOnlyList<int> positions,
        GameConfiguration configuration,
        decimal betPerLine,
        bool fromLeft)
    {
        if (symbols.Count < 3) return null;

        // Find base symbol (first non-wild from the start direction)
        string? baseSymbol = null;
        int startIndex = fromLeft ? 0 : symbols.Count - 1;
        int step = fromLeft ? 1 : -1;

        for (int i = startIndex; i >= 0 && i < symbols.Count; i += step)
        {
            if (symbols[i] != "WILD")
            {
                baseSymbol = symbols[i];
                break;
            }
        }

        // If all wilds, no win (wilds don't pay directly)
        if (baseSymbol == null) return null;

        // Count matching symbols in sequence
        int matchCount = 0;
        var winPositions = new List<int>();

        if (fromLeft)
        {
            // Left-to-right: iterate forward
            for (int i = 0; i < symbols.Count; i++)
            {
                var symbol = symbols[i];
                if (symbol == baseSymbol || symbol == "WILD")
                {
                    matchCount++;
                    winPositions.Add(positions[i]);
                }
                else
                {
                    break; // Stop at first non-matching symbol
                }
            }
        }
        else
        {
            // Right-to-left: iterate backward
            for (int i = symbols.Count - 1; i >= 0; i--)
            {
                var symbol = symbols[i];
                if (symbol == baseSymbol || symbol == "WILD")
                {
                    matchCount++;
                    winPositions.Insert(0, positions[i]); // Insert at front to maintain left-to-right order
                }
                else
                {
                    break; // Stop at first non-matching symbol
                }
            }
        }

        if (matchCount < 3) return null;

        // Find matching paytable entry
        var paytableEntry = configuration.Paytable.FirstOrDefault(e => e.SymbolCode == baseSymbol);
        if (paytableEntry == null) return null;

        // Find best multiplier for this count
        var bestMatch = paytableEntry.Multipliers
            .Where(mult => matchCount >= mult.Count)
            .OrderByDescending(mult => mult.Count)
            .FirstOrDefault();

        if (bestMatch == null) return null;

        var payout = Money.FromBet(betPerLine, bestMatch.Multiplier);
        return new SymbolWin(baseSymbol, matchCount, bestMatch.Multiplier, payout, winPositions);
    }
}

public sealed record WinEvaluationResult(Money TotalWin, IReadOnlyList<SymbolWin> SymbolWins);

