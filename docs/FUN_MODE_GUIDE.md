# Fun Mode Guide

## Overview

Fun Mode (also known as demo mode) allows players to play the game without real money. When `funMode=1` in the `/start` request, the backend uses pre-configured grids instead of RNG (Random Number Generation) to provide consistent, predictable gameplay for demonstrations.

## How It Works

### Start Request

When starting a game session, set `funMode=1`:

```json
POST /TEST/starburst/start
{
  "languageId": "en",
  "client": "desktop",
  "funMode": 1,
  "token": ""  // Optional for fun mode
}
```

### Pre-Configured Grids

The backend contains **15 pre-configured grids** stored in `FunModeGridProvider.cs`. When fun mode is active:

1. **Random Selection**: Each spin randomly selects one of the 15 pre-configured grids
2. **No RNG**: The game bypasses the RNG service and uses the selected grid directly
3. **Consistent Results**: The same grid will always produce the same win outcomes

### Grid Format

Each grid is a flat array of 15 symbol IDs in **column-major order**:
- Format: `[reel0_row0, reel0_row1, reel0_row2, reel1_row0, reel1_row1, reel1_row2, ...]`
- Symbol IDs: 0-based index in symbol catalog
  - `0` = WILD
  - `1` = BAR
  - `2` = SEVEN
  - `3` = ORANGE
  - `4` = RED
  - `5` = GREEN
  - `6` = BLUE
  - `7` = PURPLE

### Available Grids

All 15 pre-configured grids are designed to **trigger the expanding wild feature** (wilds on reels 2, 3, or 4):

**Important Rule**: Wilds can **only appear on the middle row** (row 1), never on the top row (row 0) or bottom row (row 2). They then expand to fill all rows.

1. **Wild on Reel 2 (middle row only)** - Single wild on middle row, expands to all rows, triggers respin
2. **Wild on Reel 3 (middle row only)** - Single wild on middle row, expands to all rows, triggers respin
3. **Wild on Reel 4 (middle row only)** - Single wild on middle row, expands to all rows, triggers respin
4. **Wild on Reel 2 (all rows)** - Already expanded state, triggers respin
5. **Wild on Reel 3 (all rows)** - Already expanded state, triggers respin
6. **Wild on Reel 4 (all rows)** - Already expanded state, triggers respin
7. **Wilds on Reels 2 and 3 (middle row only)** - Multiple wilds on middle row, one selected, triggers respin
8. **Wilds on Reels 3 and 4 (middle row only)** - Multiple wilds on middle row, one selected, triggers respin
9. **Wild on Reel 2 (middle row) with win potential** - Wild + BAR win combination
10. **Wild on Reel 3 (middle row) with big win** - Wild + RED symbols win
11. **Wild on Reel 4 (middle row) with mixed symbols** - Wild with colorful symbols
12. **Wild on Reel 2 (middle row) with BAR win** - Wild + BAR win
13. **Wild on Reel 3 (middle row) with BAR win** - Wild + BAR win
14. **Wild on Reel 4 (middle row) with BAR win** - Wild + BAR win
15. **All three wild positions (middle row)** - Maximum respin potential (Reels 2, 3, 4 all have wilds on middle row)

**Note**: All grids ensure the expanding wild feature is triggered, allowing users to see:
- Initial wild symbol appearance (on middle row only)
- Wild expansion to fill the entire reel (all 3 rows)
- Reel locking with expanded wilds
- Respins with locked wild reels

## Backend Implementation

### FunModeGridProvider

The `FunModeGridProvider` class in `GameEngine/Play/FunModeGridProvider.cs` provides:

- `GetRandomGrid(FortunaPrng prng)` - Returns a random grid from the 15 available using the cryptographic PRNG
- `GetGrid(int index)` - Returns a specific grid by index (0-14)
- `GetAllGrids()` - Returns all 15 grids
- `GridCount` - Returns the number of available grids (15)

### SpinHandler Integration

When `PlayRequest.FunMode = true`:

1. `SpinHandler` detects fun mode
2. Calls `FunModeGridProvider.GetRandomGrid()` to select a grid
3. Creates the board using `CreateFunModeBoard()` instead of RNG
4. Logs the selected grid for debugging

### Session Management

The `SessionRecord` stores the `FunMode` flag:

```csharp
public sealed class SessionRecord
{
    public bool FunMode { get; }  // true = fun mode, false = real money
    // ...
}
```

When creating a session via `/start`:
- `funMode=0` → `SessionRecord.FunMode = false` (uses RNG)
- `funMode=1` → `SessionRecord.FunMode = true` (uses pre-configured grids)

## Testing Fun Mode

### Using Postman or cURL

1. **Start a fun mode session:**
```http
POST http://localhost:5101/TEST/starburst/start
Content-Type: application/json

{
  "languageId": "en",
  "client": "desktop",
  "funMode": 1,
  "token": ""
}
```

2. **Play a spin:**
```http
POST http://localhost:5101/TEST/starburst/play
Content-Type: application/json

{
  "sessionId": "your-session-id-from-start",
  "baseBet": 1.0,
  "betMode": "standard",
  "bets": [
    { "betType": "base", "amount": 1.0 }
  ]
}
```

The backend will automatically use one of the 15 pre-configured grids.

### Console Logs

When fun mode is active, you'll see logs like:

```
[SpinHandler] FUN MODE: Using pre-configured grid instead of RNG
[SpinHandler] Selected fun mode grid (random from 15 available grids)
[SpinHandler] Created FUN MODE board with pre-configured grid
[SpinHandler] ===== FUN MODE GRID LAYOUT =====
[SpinHandler] Column-major order (as provided):
[SpinHandler]   [1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 1]
[SpinHandler] Visual representation (5 columns × 3 rows):
[SpinHandler]   TOP ROW: [BAR(1) | BAR(1) | BAR(1) | BAR(1) | BAR(1)]
[SpinHandler]   MID ROW: [BAR(1) | BAR(1) | WILD(0) | BAR(1) | BAR(1)]
[SpinHandler]   BOT ROW: [BAR(1) | BAR(1) | WILD(0) | BAR(1) | BAR(1)]
[SpinHandler] =================================
```

## Frontend Integration

The frontend receives `funMode` in the `/start` response:

```json
{
  "statusCode": 6000,
  "game": {
    "funMode": true,  // ← This tells frontend it's fun mode
    "sessionId": "...",
    // ...
  }
}
```

The frontend can use this to:
- Show "DEMO" or "FUN MODE" UI indicators
- Display unlimited balance
- Disable real money features
- Show different UI styling

## Configuration

### Force Fun Mode (Testing)

You can force all sessions to use fun mode via `appsettings.json`:

```json
{
  "ForceFunMode": true
}
```

Or via environment variable:
```bash
FORCE_FUN_MODE=true
```

This overrides the `funMode` parameter in the request and forces all sessions to use fun mode.

## Differences from Real Money Mode

| Feature | Real Money (funMode=0) | Fun Mode (funMode=1) |
|---------|------------------------|----------------------|
| **Grid Generation** | RNG (Random Number Generation) | Pre-configured grids (random selection from 15) |
| **Balance** | Real player balance | Demo balance (default 10000) |
| **Token Required** | ✅ Yes | ❌ Optional |
| **RNG Service** | ✅ Uses external RNG | ❌ Bypassed |
| **Consistency** | Random every spin | Predictable (15 grids) |
| **Use Case** | Production gameplay | Testing, demos, tutorials |

## Security Considerations

⚠️ **Important**: Fun mode is for demonstration purposes only. In production:

1. **Validate fun mode on backend** - Never trust frontend `funMode` flag
2. **Separate sessions** - Fun mode sessions should not interact with real money sessions
3. **No real payouts** - Fun mode wins should never be credited to real accounts
4. **Rate limiting** - Consider rate limiting fun mode requests to prevent abuse

## Adding More Grids

To add more pre-configured grids, edit `FunModeGridProvider.cs`:

```csharp
private static readonly IReadOnlyList<IReadOnlyList<int>> FunModeGrids = new[]
{
    // ... existing grids ...
    // Grid 16: Your new grid
    new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
    // ...
};
```

The `GetRandomGrid()` method will automatically include the new grids in random selection.
