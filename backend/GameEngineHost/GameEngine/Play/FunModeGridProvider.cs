using System.Collections.Generic;
using System.Linq;
using GameEngine.Services;

namespace GameEngine.Play;

/// <summary>
/// Provides pre-configured grids for fun mode (demo mode).
/// When funMode=1, the game randomly selects from these 15 pre-configured grids
/// instead of using RNG to generate random symbols.
/// 
/// IMPORTANT: Wilds must appear ONLY on the middle row (row 1), never on the top (row 0) or bottom (row 2).
/// They will then expand to fill all rows during the expanding wild feature.
/// </summary>
public static class FunModeGridProvider
{
    /// <summary>
    /// 15 pre-configured grids for fun mode demonstrations.
    /// Each grid is 15 symbols in column-major order: [reel0_row0, reel0_row1, reel0_row2, reel1_row0, ...]
    /// Grid layout: Reel0 (0-2), Reel1 (3-5), Reel2 (6-8), Reel3 (9-11), Reel4 (12-14)
    /// Row layout: row0=top, row1=middle, row2=bottom
    /// Expanding wilds trigger on Reels 2, 3, 4 (1-based; 0-based indices 1-3)
    /// IMPORTANT RULES:
    /// 1. Most grids have NO wilds (regular base-game wins).
    /// 2. Exactly 3 grids include a SINGLE WILD to demonstrate the expanding-wilds + respin feature.
    /// 3. If a grid contains a WILD, it must be ONLY ONE wild and it must be on the middle row (row1) of reels 2, 3, or 4.
    ///    Valid WILD positions (column-major): Index 4 (Reel 2, middle), Index 7 (Reel 3, middle), Index 10 (Reel 4, middle)
    ///
    /// Symbol IDs (index in `symbolCatalog`, see `configs/starburst.json`):
    /// 0=WILD, 1=BAR, 2=SEVEN, 3=RED, 4=PURPLE, 5=BLUE, 6=GREEN, 7=ORANGE
    /// </summary>
    private static readonly IReadOnlyList<IReadOnlyList<int>> FunModeGrids = new[]
    {
        // Grid 1: Demonstrate Payline 1 win (middle row straight) - SEVEN x5 on payline 1
        new[] { 6, 7, 3, 6, 7, 3, 6, 7, 3, 7, 2, 5, 5, 2, 7 },

        // Grid 2: Demonstrate Payline 2 win (top row straight) - ORANGE x5 on payline 2
        new[] { 7, 2, 6, 7, 5, 4, 7, 3, 5, 7, 6, 2, 7, 4, 3 },

        // Grid 3: Demonstrate Payline 3 win (bottom row straight) - GREEN x5 on payline 3
        new[] { 3, 2, 6, 7, 5, 6, 4, 3, 6, 5, 7, 6, 2, 4, 6 },

        // Grid 4: Demonstrate Payline 4 win (V-shape up) - PURPLE x5 on payline 4
        new[] { 6, 7, 3, 6, 7, 3, 3, 4, 5, 4, 5, 6, 5, 6, 7 },

        // Grid 5: Demonstrate Payline 5 win (V-shape down) - BLUE x5 on payline 5
        new[] { 6, 7, 3, 7, 3, 4, 5, 6, 7, 4, 5, 6, 3, 4, 5 },

        // Grid 6: Demonstrate Payline 6 win (top-top-middle-top-top) - GREEN x5 on payline 6
        new[] { 5, 6, 7, 5, 6, 7, 4, 5, 6, 5, 6, 7, 5, 6, 7 },

        // Grid 7: Demonstrate Payline 7 win (bottom-bottom-middle-bottom-bottom) - RED x5 on payline 7
        new[] { 7, 3, 4, 7, 3, 4, 3, 4, 5, 5, 6, 7, 7, 3, 4 },

        // Grid 8: Demonstrate Payline 8 win (bottom-heavy) - PURPLE x5 on payline 8
        new[] { 6, 4, 2, 3, 7, 4, 5, 2, 4, 7, 6, 4, 2, 4, 5 },

        // Grid 9: Demonstrate Payline 9 win (top-heavy) - BLUE x5 on payline 9
        new[] { 3, 5, 7, 5, 2, 4, 5, 7, 2, 5, 6, 3, 4, 5, 6 },

        // Grid 10: Demonstrate Payline 10 win (alternating) - ORANGE x5 on payline 10
        new[] { 4, 7, 2, 7, 5, 6, 3, 7, 5, 7, 2, 4, 6, 7, 3 },

        // Grid 11: No-wins demo (no wild) - mixed symbols, designed to avoid any 3+ in-a-row payline wins
        new[] { 3, 2, 6, 6, 5, 4, 4, 7, 3, 7, 6, 5, 5, 3, 7 },

        // Grid 12: Multiple wins demo (no wild) - multiple paylines win, but not a full-screen single symbol
        // Top row: RED x5 (payline 2), Middle row: BAR x5 (payline 1), Bottom row: SEVEN x5 (payline 3)
        new[] { 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2 },

        // Grid 13: Feature demo (WILD on Reel 2 middle row) - triggers expanding wild + respin
        // WILD at index 4 (column-major): Reel 2 (1-based), middle row
        new[] { 3, 2, 6, 7, 0, 4, 5, 2, 7, 4, 2, 5, 6, 2, 3 },

        // Grid 14: Feature demo (WILD on Reel 3 middle row) - triggers expanding wild + respin
        // WILD at index 7 (column-major): Reel 3 (1-based), middle row
        new[] { 1, 4, 6, 1, 5, 7, 3, 0, 2, 1, 2, 5, 1, 6, 4 },

        // Grid 15: Feature demo (WILD on Reel 4 middle row) - triggers expanding wild + respin
        // WILD at index 10 (column-major): Reel 4 (1-based), middle row
        new[] { 3, 6, 2, 5, 3, 7, 4, 1, 3, 2, 0, 5, 3, 4, 6 }
    };

    /// <summary>
    /// Gets a random fun mode grid from the pre-configured list.
    /// Validates that any wild (if present) only appears on the middle row (row 1) of reels 2, 3, or 4.
    /// </summary>
    public static IReadOnlyList<int> GetRandomGrid(FortunaPrng prng)
    {
        // In fun mode we don't want a single bad demo grid to "jam" gameplay.
        // Try multiple times and return the first valid grid.
        for (var attempt = 0; attempt < FunModeGrids.Count; attempt++)
        {
            var index = prng.NextInt32(0, FunModeGrids.Count);
            var grid = FunModeGrids[index];

            try
            {
                // Validate grid: wilds must only be on middle row (row 1) of reels 2, 3, or 4 (1-based)
                // Middle-row indices (column-major): Reel 2 => index 4, Reel 3 => index 7, Reel 4 => index 10
                ValidateGrid(grid);
                return grid;
            }
            catch
            {
                // Ignore and re-roll.
            }
        }

        throw new InvalidOperationException("No valid fun mode grid available. Please fix FunModeGrids.");
    }
    
    /// <summary>
    /// Validates that a grid has EITHER:
    /// - 0 wilds (regular base-game demo grids), OR
    /// - exactly 1 wild on the middle row (row 1) of reels 2, 3, or 4 (feature demo grids)
    /// Throws exception if:
    /// - More than one wild is found
    /// - Wild is found in invalid positions (wrong reel or wrong row)
    /// Grid is column-major: [reel0_row0, reel0_row1, reel0_row2, reel1_row0, ...]
    /// Valid positions (ONLY ONE allowed):
    /// - Index 4: Reel 2, middle row (row 1) - VALID
    /// - Index 7: Reel 3, middle row (row 1) - VALID
    /// - Index 10: Reel 4, middle row (row 1) - VALID
    /// NOTE: Wilds must ONLY be on Reels 2, 3, or 4 (NOT Reel 1 or Reel 5)
    /// </summary>
    private static void ValidateGrid(IReadOnlyList<int> grid)
    {
        const int WILD_ID = 0;
        const int MIN_SYMBOL_ID = 0;
        const int MAX_SYMBOL_ID = 7; // Starburst has 8 symbols in configs/starburst.json (IDs 0-7)

        // Validate symbol ID range first (prevents runtime errors during board creation)
        for (int i = 0; i < grid.Count; i++)
        {
            if (grid[i] < MIN_SYMBOL_ID || grid[i] > MAX_SYMBOL_ID)
            {
                throw new InvalidOperationException(
                    $"Invalid symbol ID {grid[i]} at index {i}. Valid range is {MIN_SYMBOL_ID}-{MAX_SYMBOL_ID} for Starburst fun mode grids.");
            }
        }

        var wildPositions = new List<(int index, int reelIndex, int rowIndex)>();
        
        // Find all wild positions
        for (int i = 0; i < grid.Count; i++)
        {
            if (grid[i] == WILD_ID)
            {
                int reelIdx = i / 3; // Column-major: reel = index / rows (0-based)
                int rowIdx = i % 3;   // Column-major: row = index % rows (0-based)
                wildPositions.Add((i, reelIdx, rowIdx));
            }
        }
        
        // Rule 1: Allow 0 wilds (non-feature demo grids)
        if (wildPositions.Count == 0)
        {
            return;
        }
        
        if (wildPositions.Count > 1)
        {
            var positions = string.Join(", ", wildPositions.Select(w => $"Index {w.index} (Reel {w.reelIndex + 1}, Row {w.rowIndex + 1})"));
            throw new InvalidOperationException(
                $"Fun mode grid must have at most ONE wild symbol, but found {wildPositions.Count} wilds: {positions}. " +
                $"Only one wild is allowed per grid, and it must be on the middle row of reels 2, 3, or 4.");
        }
        
        // Rule 2: The single wild must be in a valid position
        var (wildIndex, wildReel, wildRow) = wildPositions[0];
        
        // Valid positions (middle row only) for allowed reels 2-4 (1-based):
        // Reel 2 => index 4, Reel 3 => index 7, Reel 4 => index 10
        if (wildIndex != 4 && wildIndex != 7 && wildIndex != 10)
        {
            throw new InvalidOperationException(
                $"Invalid wild position: Index {wildIndex} (Reel {wildReel + 1}, Row {wildRow + 1}). " +
                $"Wilds must only appear on middle row (row 2) of reels 2, 3, or 4. " +
                $"NOT allowed on Reel 1 (first reel) or Reel 5 (last reel). " +
                $"Valid positions: Index 4 (Reel 2, middle), Index 7 (Reel 3, middle), Index 10 (Reel 4, middle).");
        }
        
        // Rule 3: Must be on middle row (row 1, 0-based)
        if (wildRow != 1)
        {
            throw new InvalidOperationException(
                $"Invalid wild row: Index {wildIndex} is on Row {wildRow + 1}. " +
                $"Wilds must only appear on the middle row (row 2).");
        }
        
        // Rule 4: Must be on reels 2, 3, or 4 ONLY (0-based reel indices 1, 2, 3)
        // NOT allowed: Reel 0 (Reel 1) or Reel 4 (Reel 5)
        if (wildReel != 1 && wildReel != 2 && wildReel != 3)
        {
            throw new InvalidOperationException(
                $"Invalid wild reel: Index {wildIndex} is on Reel {wildReel + 1}. " +
                $"Wilds must only appear on reels 2, 3, or 4. " +
                $"NOT allowed on Reel 1 (first reel) or Reel 5 (last reel).");
        }
    }

    /// <summary>
    /// Gets a specific fun mode grid by index (0-14).
    /// </summary>
    public static IReadOnlyList<int> GetGrid(int index)
    {
        if (index < 0 || index >= FunModeGrids.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Grid index must be between 0 and {FunModeGrids.Count - 1}.");
        }
        return FunModeGrids[index];
    }

    /// <summary>
    /// Gets all available fun mode grids.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> GetAllGrids() => FunModeGrids;

    /// <summary>
    /// Gets the count of available fun mode grids.
    /// </summary>
    public static int GridCount => FunModeGrids.Count;
}
