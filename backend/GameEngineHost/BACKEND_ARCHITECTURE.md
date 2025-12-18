# Backend Architecture & Game Logic Documentation

## Backend Structure

### Architecture Overview

The backend is built as an **ASP.NET Core Web API** application written in C#. It follows a clean architecture pattern with clear separation between API controllers, game engine logic, and configuration.

### Key Components

```
backend/GameEngineHost/
├── Controllers/
│   └── PlayController.cs          # HTTP API endpoint
├── GameEngine/
│   ├── Play/
│   │   ├── GameEngineService.cs   # Main service interface
│   │   ├── SpinHandler.cs         # Core game logic orchestrator
│   │   ├── WinEvaluator.cs        # Win calculation math
│   │   └── PlayContracts.cs       # Request/Response DTOs
│   ├── Configuration/
│   │   └── GameConfigurationLoader.cs  # Loads configs & reel sets
│   └── Services/
│       └── FortunaPrng.cs         # Random number generator
└── configs/
    ├── starburst.json             # Main game configuration
    └── starburstReelsets.json     # Reel strip definitions
```

### Request Flow

1. **`PlayController.PlayAsync()`** - Receives HTTP POST request to `/play` endpoint
2. **`IEngineClient.PlayAsync()`** - Wraps the game engine service (can be local or remote)
3. **`GameEngineService.PlayAsync()`** - Delegates to `SpinHandler`
4. **`SpinHandler.PlayAsync()`** - Orchestrates the entire spin:
   - Loads game configuration
   - Selects appropriate reel strips
   - Creates game board from reel strips
   - Evaluates wins using paylines
   - Handles cascading wins
   - Manages free spins state
   - Returns complete response

### Service Registration

Services are registered in `Program.cs` and wired together via `ServiceCollectionExtensions.AddGameEngine()`:

- **GameConfigurationLoader** - Singleton, loads and caches game configs
- **SpinHandler** - Singleton, core game logic
- **WinEvaluator** - Singleton, win calculation
- **FortunaPrng** - Singleton, fallback random number generator
- **IRngClient** - Integrated RNG service (uses FortunaPrng)

---

## Reel Sets Configuration

### Location

Reel sets are defined in: **`configs/starburstReelsets.json`**

### Structure

The reel sets are defined as arrays of symbol codes (e.g., `"Sym1"`, `"Sym2"`):

```json
{
  "reelsetHigh": [...],      // High RTP reel set
  "reelsetLow": [...],       // Low RTP reel set  
  "reelsetBB": [...],        // Buy bonus reel set
  "reelsetFreeSpins": [...]  // Free spins reel set
}
```

Each reel set contains **5 arrays** (one per reel/column), where each array is a strip of symbols that can appear on that reel.

### Configuration Reference

Reel sets are referenced in **`configs/starburst.json`** (lines 122-129):

```json
"reels": {
  "sourceFile": "configs/starburstReelsets.json",
  "keys": {
    "high": "reelsetHigh",
    "low": "reelsetLow",
    "buy": "reelsetBB",
    "freeSpins": "reelsetFreeSpins"
  }
}
```

### Loading Process

1. **`GameConfigurationLoader.LoadReelDataAsync()`** (lines 88-131):
   - Reads the JSON file specified in `reels.sourceFile`
   - Maps the keys (`high`, `low`, `buy`, `freeSpins`) to reel strip arrays
   - Stores them in a `ReelLibrary` object attached to the configuration

2. **`SpinHandler.SelectReelStrips()`** (lines 348-359):
   - **Free Spins mode** → Uses `reelsetFreeSpins`
   - **Buy Entry mode** → Uses `reelsetBB`
   - **Base Game** → Weighted selection between `reelsetHigh` and `reelsetLow`

### Reel Selection Logic

For base game spins, the system uses **weighted random selection** (`SpinHandler.SelectBaseReels()`, lines 361-379):

1. Reads weights from `betModes.standard.reelWeights` (e.g., `low: 70, high: 30`)
2. Calculates total weight (70 + 30 = 100)
3. Generates random number between 0 and total weight
4. If roll < lowWeight → returns `reelsetLow`
5. Otherwise → returns `reelsetHigh`

This allows control over RTP by adjusting the probability of high vs low reel sets.

### Symbol Mapping

Symbols in reel strips use internal codes (e.g., `"Sym1"`, `"Sym2"`) which are mapped to display codes (e.g., `"WILD"`, `"BAR"`) via the `SymbolCatalog` in `starburst.json`:

```json
"symbolCatalog": [
  { "sym": "Sym1", "code": "WILD", "displayName": "Wild", "type": "High" },
  { "sym": "Sym2", "code": "BAR", "displayName": "Bar", "type": "High" },
  ...
]
```

---

## Math & Game Logic

### 1. Board Creation

**Location**: `SpinHandler.ReelBoard.Create()` (lines 678-707)

**Process**:
1. Takes the selected reel strips (5 arrays, one per column)
2. Uses RNG seeds to determine start position for each reel
3. For each reel:
   - Takes the start index (from RNG seed modulo reel length)
   - Creates 3 symbols starting from that position
   - Wraps around if reaching end of strip
4. Creates a 5 columns × 3 rows board
5. Each symbol can have a multiplier assigned (if it's a multiplier symbol type)

**RNG Seeds**: 
- Fetched from external RNG service (or fallback to FortunaPrng)
- One seed per reel for start position
- Additional seeds for multiplier assignment

### 2. Win Evaluation

**Location**: `WinEvaluator.Evaluate()` (lines 24-111)

**Paylines**: 10 fixed paylines defined (lines 10-22):
```csharp
new[] { 1, 1, 1, 1, 1 }, // Payline 1: Middle row
new[] { 0, 0, 0, 0, 0 }, // Payline 2: Top row
new[] { 2, 2, 2, 2, 2 }, // Payline 3: Bottom row
new[] { 0, 1, 2, 1, 0 }, // Payline 4: V shape
new[] { 2, 1, 0, 1, 2 }, // Payline 5: Inverted V
new[] { 0, 0, 1, 0, 0 }, // Payline 6: Top-center
new[] { 2, 2, 1, 2, 2 }, // Payline 7: Bottom-center
new[] { 1, 2, 2, 2, 1 }, // Payline 8: Bottom-heavy
new[] { 1, 0, 0, 0, 1 }, // Payline 9: Top-heavy
new[] { 1, 0, 1, 0, 1 }  // Payline 10: Alternating
```

**Evaluation Process**:
1. **Bet per line** = Total bet ÷ 10 paylines
2. For each payline:
   - Extract symbols along the payline path
   - Evaluate **left-to-right** direction
   - Evaluate **right-to-left** direction
   - Choose the **best win** from both directions
3. **Wild substitution**: WILD can substitute for any symbol
4. **Base symbol**: First non-wild symbol determines the win type
5. **Match count**: Count consecutive matching symbols (including wilds)
6. **Paytable lookup**: Match against paytable (requires 3+ of a kind)
7. **Payout calculation**: `betPerLine × paytableMultiplier`
8. **Highest win per payline**: Only the highest win on each payline is paid

**Paytable** (from `starburst.json`):
- BAR: 3→10x, 4→25x, 5→50x
- SEVEN: 3→5x, 4→12x, 5→25x
- ORANGE: 3→2x, 4→5x, 5→12x
- GREEN: 3→1.6x, 4→4x, 5→10x
- RED: 3→1.4x, 4→3x, 5→8x
- BLUE: 3→1x, 4→2x, 5→5x
- PURPLE: 3→1x, 4→2x, 5→5x
- WILD: No direct payout (substitute only)

### 3. Cascade Logic

**Location**: `SpinHandler.PlayAsync()` (lines 99-200)

**Cascade Loop** (max 50 iterations for safety):
1. **Evaluate wins** on current board state
2. **If no wins** → break cascade loop
3. **Remove winning symbols** from board
4. **Gravity effect**: Symbols fall down to fill empty spaces
5. **Refill**: Empty spaces at top are filled from reel strips (continuing from last position)
6. **Apply multipliers** (if any multiplier symbols present)
7. **Repeat** until no more wins

**Multiplier Application**:
- **Base Game**: Multipliers on board are summed and applied to cascade win
- **Free Spins**: Multipliers accumulate in `TotalMultiplier` and apply to all wins

### 4. Multiplier Assignment

**Location**: `SpinHandler.AssignMultiplierValue()` (lines 459-512)

**Process**:
1. Only applies to symbols with `SymbolType.Multiplier`
2. Uses **weighted random selection** based on game mode:
   - **Standard bet mode**: Uses `multiplier.weights.standard`
   - **Ante bet mode**: Uses `multiplier.weights.ante`
   - **Free spins**: 
     - If `TotalMultiplier < freeSpinsSwitchThreshold` (250) → `freeSpinsHigh` profile
     - Otherwise → `freeSpinsLow` profile

**Multiplier Values**: 2x, 3x, 4x, 5x (from `starburst.json`)

**Weight Example** (Standard mode):
- 2x: 50% weight
- 3x: 30% weight
- 4x: 15% weight
- 5x: 5% weight

### 5. Special Rules

#### Single Wild Reel Rule

**Location**: `SpinHandler.EnforceSingleWildReel()` (lines 558-594)

**Rule**: Only **ONE reel** (reels 2, 3, or 4 - indices 1, 2, 3) can have wilds per spin.

**Process**:
1. Check which reels (2, 3, 4) contain wild symbols
2. If multiple reels have wilds:
   - Randomly select one reel to keep wilds
   - Replace wilds on other reels with random non-wild symbols
3. This ensures only one expanding wild reel per spin

### 6. Money Calculations

**Money Type**: All money uses `Money` struct with `decimal(20,2)` precision

**Calculations**:
- **Payout per win**: `betPerLine × paytableMultiplier`
- **Cascade win**: `baseWin × multiplierSum` (if multipliers present)
- **Total win**: Sum of all cascade wins
- **Max win cap**: `bet × maxWinMultiplier` (500x from config)
- **Final win**: `min(totalWin, maxWin)`

**Precision**: All calculations use `decimal.Round()` with 2 decimal places (MidpointRounding.ToEven)

### 7. Free Spins Logic

**Trigger**: Scatter symbols (currently not configured in Starburst)

**State Management**:
- `EngineSessionState.FreeSpins` tracks:
  - `SpinsRemaining`: Number of free spins left
  - `TotalMultiplier`: Accumulated multiplier across all free spins
  - `FeatureWin`: Total win during free spins feature
  - `JustTriggered`: Flag for first spin of feature

**Multiplier Accumulation**: 
- In free spins, multipliers accumulate across spins
- Applied to all wins during the feature
- Resets when feature ends

### 8. Random Number Generation

**Primary**: External RNG service via `IRngClient` (for regulatory compliance)

**Fallback**: `FortunaPrng` (cryptographically secure PRNG)

**Seeds Used**:
- **Reel start positions**: One seed per reel (5 seeds)
- **Multiplier assignment**: One seed per board position (15 seeds for 5×3 grid)

**RNG Request** (`SpinHandler.FetchRandomContext()`, lines 381-432):
- Creates pools for "reel-starts" and "multiplier-seeds"
- Includes metadata (reel lengths, multiplier values)
- Tracks round ID, player token, mode, bet mode

---

## Configuration Files

### starburst.json

Main game configuration containing:
- **Board dimensions**: 5 columns × 3 rows
- **Symbol catalog**: 8 symbols with codes and types
- **Paytable**: Win multipliers for each symbol (3, 4, 5 of a kind)
- **Bet modes**: Standard and Ante with reel weights
- **Multiplier configuration**: Values and weights per mode
- **Reel references**: Points to reel sets file
- **Max win multiplier**: 500x

### starburstReelsets.json

Reel strip definitions:
- **reelsetHigh**: High RTP configuration
- **reelsetLow**: Low RTP configuration
- **reelsetBB**: Buy bonus entry
- **reelsetFreeSpins**: Free spins mode

Each reel set is an array of 5 arrays (one per reel), containing symbol codes.

---

## Key Design Principles

1. **Data-Driven**: All game parameters (RTP, payouts, reel compositions) are in JSON configs
2. **Deterministic**: Same inputs (seeds, config) produce same outputs
3. **Regulatory Compliance**: Uses external RNG service for provably fair gaming
4. **Separation of Concerns**: Configuration, logic, and API are cleanly separated
5. **Caching**: Game configurations are loaded once and cached
6. **Type Safety**: Strong typing with C# records and structs
7. **Precision**: Money calculations use fixed decimal precision

---

## Summary

The backend is structured as a clean, maintainable ASP.NET Core application with:
- **Configuration-driven** game parameters
- **Modular** game engine components
- **Deterministic** math based on RNG seeds
- **Cascading win** system with multiplier support
- **10-payline** evaluation with bidirectional matching
- **Weighted reel selection** for RTP control
- **State management** for free spins and features

All game logic is centralized in `SpinHandler` with clear separation between board creation, win evaluation, and cascade processing.

