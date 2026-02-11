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

## API Endpoints

### 1. Game Engine Host (GameEngineHost)

#### 1.1 Health Check

- **Method**: `GET`
- **Path**: `/health`
- **Location**: `Program.cs` (health check mapping)
- **Purpose**: Simple liveness/readiness probe for Kubernetes and local diagnostics.

**Response (200 OK)**:

```json
{
  "status": "healthy"
}
```

No request body is required. If the process is running and the ASP.NET pipeline is up, this endpoint returns HTTP 200.

#### 1.2 Play – Starburst Spin Evaluation

- **Method**: `POST`
- **Path**: `/play`
- **Location**: `Controllers/PlayController.cs`
- **Purpose**: Evaluate a single Starburst spin (base game or respin) and return the full math outcome.

##### Request Body (`PlayRequest`)

Shape (simplified – see `PlayContracts.cs` for full definition):

- **`gameId`** (`string`, required): Must be `"starburst"` for this game.
- **`playerToken`** (`string`, required): Platform/player identifier (opaque to engine).
- **`bets`** (`BetRequest[]`, required):
  - `amount` (`Money`, required): Per-line or total bet entry (per RGS spec only `amount` is guaranteed).
  - `betType` (`string?`, optional): Logical bet type (may be omitted by platform).
- **`baseBet`** (`Money`, required): Base bet per spin (usually per-line bet × lines).
- **`totalBet`** (`Money`, required): Total bet for the spin (used if `bet` is not provided).
- **`betMode`** (`BetMode`, required): `Standard` or `Ante` (Starburst currently uses `Standard`).
- **`isFeatureBuy`** (`bool`, required): `true` if this is a feature-buy entry spin (currently mapped to same reel set).
- **`engineState`** (`EngineSessionState?`, optional): Engine state from previous response. Used to continue respin feature.
  - `respins` (`RespinState?`): Tracks `RespinsRemaining`, `LockedWildReels`, etc.
- **`userPayload`** (`JsonElement?`, optional): Custom payload (used for fun-mode custom grids, etc.).
- **`lastResponse`** (`JsonElement?`, optional): Previous `PlayResponse` forwarded by platform; used as a fallback to restore state.
- **`bet`** (`Money?`, optional): Total bet alias per RGS spec; if absent, engine can derive from `bets` / `totalBet`.
- **`rtpLevel`**, **`mode`**, **`currency`**, **`funMode`**: Additional fields per RGS spec (funMode uses fixed demo grids instead of RNG).

Engine-side validation is performed in `SpinHandler.ValidateRequest()`:
- Ensures `gameId` is present.
- Ensures at least one `bets` entry exists.
- Ensures an effective bet amount (`bet`, `totalBet`, or sum of `bets`) is positive.

##### Successful Response (`PlayResponse`, HTTP 200)

On success, the engine returns:

- **`statusCode`** (`int`): `200` for success.
- **`win`** (`Money`): Total win for this step (base win + feature win, after multipliers and caps).
- **`scatterWin`** (`Money`): Scatter win (always zero for Starburst – no scatter symbols).
- **`featureWin`** (`Money`): Win attributed to the expanding wild feature (currently zero – no separate feature accounting).
- **`buyCost`** (`Money`): Cost if this was a feature-buy spin; otherwise zero.
- **`freeSpins`** (`int`): Number of free spins awarded (always `0` for Starburst – no free spins feature).
- **`roundId`** (`string`): Unique round identifier for this request.
- **`timestamp`** (`DateTimeOffset`): Server time when the spin was processed.
- **`nextState`** (`EngineSessionState`): Engine state to be passed back on the next request:
  - `respins`: Tracks Wild Respin feature state (remaining respins, locked reels, etc.).
- **`results`** (`ResultsEnvelope`): Math outcome envelope forwarded to the frontend via RGS:
  - `cascades` (`CascadeStep[]`): Single entry representing before/after wild expansion (for animation).
  - `wins` (`SymbolWin[]`): List of line wins with symbol codes, counts, multipliers, payouts and coordinates.
  - `scatter`: Always `null` in Starburst.
  - `freeSpins`: Always `null` in Starburst.
  - `rngTransactionId`: Round/transaction ID (matches `roundId`).
  - `finalGridSymbols`: Flattened symbol IDs for the final 5×3 board.
  - `stops`: Reel stop positions `[5]` – one stop index per reel.
  - `totalWin`: Duplicate of top-level win for frontend convenience.
  - `feature`: Feature state (`ResultsFeature`) for the expanding wilds respin feature.
- **`message`** (`string`): Human-readable description of the status (`"Request processed successfully"` or validation message).
- **`feature`** (`FeatureOutcome?`): Top-level feature outcome (type `"EXPANDING_WILDS"` when feature is active).

##### Error Responses

Errors are mapped in `PlayController` using standard HTTP status codes and a simple JSON envelope:

- **400 Bad Request** — invalid arguments (e.g. missing `gameId`, non-positive bet):

```json
{
  "statusCode": 400,
  "message": "Bet amount must be positive. Provide 'bet', 'totalBet', or bets with amounts.",
  "parameter": "bet"
}
```

- **500 Internal Server Error** — configuration or runtime errors (missing config file, invalid operation, unexpected exception):

```json
{
  "statusCode": 500,
  "message": "Configuration file not found: configs/starburst.json"
}
```

Swagger/OpenAPI is enabled in `Program.cs`, so when running locally you can browse and test endpoints via:

- **Swagger UI**: `GET /swagger`

## Reel Sets Configuration

### Location

Reel sets are defined in: **`configs/starburstReelsets.json`**

### Structure

The reel sets are defined as arrays of symbol codes (e.g., `"Sym1"`, `"Sym2"`):

```json
{
  "reelsetBase": [...]       // Currently the only reel set used for all modes
}
```

**Note**: The system can support multiple reel sets (e.g., `reelsetHigh`, `reelsetLow`, `reelsetBB`, `reelsetFreeSpins`), but currently only `reelsetBase` is defined and used.

Each reel set contains **5 arrays** (one per reel/column), where each array is a strip of symbols that can appear on that reel. Currently, each reel has **243 symbols**.

### Configuration Reference

Reel sets are referenced in **`configs/starburst.json`** (lines 100-107):

```json
"reels": {
  "sourceFile": "configs/starburstReelsets.json",
  "keys": {
    "high": "reelsetBase",
    "low": "reelsetBase",
    "buy": "reelsetBase"
  }
}
```

**Current Implementation**: The game currently uses a **single reel set** (`reelsetBase`) for all modes. All keys (`high`, `low`, `buy`) point to the same reel set, providing a unified experience across all game modes.

**Future Flexibility**: The system architecture supports multiple reel sets if needed. You can configure different reel sets for:
- `high` / `low` - Different RTP configurations for base game
- `buy` - Special reel set for buy bonus feature
- `freeSpins` - Special reel set for free spins (if implemented)

### Loading Process

1. **`GameConfigurationLoader.LoadReelDataAsync()`** (lines 88-131):
   - Reads the JSON file specified in `reels.sourceFile`
   - Maps the keys (`high`, `low`, `buy`) to reel strip arrays from the reel sets file
   - Stores them in a `ReelLibrary` object attached to the configuration
   - Currently all keys resolve to `reelsetBase`, but can be configured to use different sets

2. **`SpinHandler.SelectReelStrips()`** (lines 680-687):
   - **All modes** → Currently uses `reelsetBase` (via `ReelLibrary.High`)
   - The method is designed to support different reel sets per mode, but currently returns the same set for all modes
   - Can be extended to use weighted selection or mode-specific sets if needed

### Reel Selection Logic

**Current Behavior**: All spins use the same `reelsetBase` reel set regardless of mode or bet mode.

**Potential Future Enhancement**: The system architecture supports **weighted random selection** for RTP control:

1. Configure different reel sets for `high` and `low` in the reel sets file
2. Update `starburst.json` keys to point to different sets
3. Implement weighted selection logic in `SelectReelStrips()`:
   - Read weights from `betModes.standard.reelWeights` (e.g., `low: 70, high: 30`)
   - Calculate total weight (70 + 30 = 100)
   - Generate random number between 0 and total weight
   - If roll < lowWeight → returns `reelsetLow`
   - Otherwise → returns `reelsetHigh`

This would allow control over RTP by adjusting the probability of high vs low reel sets, but is not currently implemented.

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

### 3. Base Spin Flow

**Location**: `SpinHandler.PlayAsync()` (lines 118-431)

**Process**:
1. **Select reel strips** based on spin mode
2. **Generate random seeds** for reel start positions
3. **Create board** from reel strips using start positions
4. **Detect wilds** on reels 2, 3, 4
5. **Enforce single wild reel rule** (if multiple wild reels detected)
6. **Expand wilds** (if any detected) - wilds expand to fill entire reel
7. **Evaluate wins** on final board state using 10 paylines
8. **Calculate total win** from all payline wins
9. **Handle respin feature** (if wilds detected, award respins for next request)

### 4. Expanding Wilds Feature

**Location**: `SpinHandler.PlayAsync()` (lines 258-475)

#### Feature Trigger (Base Game)

**When**: Wild symbol appears on reels 2, 3, or 4 during base game spin

**Process**:
1. **Detect wild reel** (only one wild reel allowed per spin)
2. **Expand wild** - fills entire reel (all 3 rows)
3. **Lock reel** - wild reel becomes locked
4. **Award respin** - `RespinsRemaining = 1` (one respin per wild reel, max 3 total)
5. **Win evaluation** - wins calculated with expanded wilds
6. **State saved** - respin state returned in `NextState` for next request

#### Respin Execution

**When**: `RespinsRemaining > 0` in engine state (on subsequent request)

**Process**:
1. **Preserve locked reels** - locked wild reels don't spin, stay as wilds
2. **Spin non-locked reels** - only reels without wilds spin
3. **Detect new wilds** - check if new wilds appeared on non-locked reels
4. **Enforce single wild reel rule** (if multiple new wild reels detected)
5. **Expand new wilds** - new wilds expand to fill entire reel
6. **Lock new wild reels** - add to locked reels set
7. **Award additional respins** - each new wild reel awards 1 respin (max 3 total)
8. **Decrement respin count** - `RespinsRemaining -= 1`
9. **Win evaluation** - wins calculated with all wilds (locked + new)

#### Feature End

**When**: `RespinsRemaining` reaches 0

**Process**:
- Respin state kept with `RespinsRemaining = 0` for one response (signals feature closure)
- On next base game spin, respin state is cleared

#### Rules

- **Wild reel restriction**: Wilds can ONLY appear on reels 2, 3, 4 (indices 1, 2, 3)
- **Single wild reel**: Only one reel can have wilds per spin (enforced by `EnforceSingleWildReel()`)
- **Maximum respins**: 3 respins total (one per wild reel)
- **Locked reels**: Wild reels stay locked during all respins

### 5. Money Calculations

**Money Type**: All money uses `Money` struct with `decimal(20,2)` precision

**Calculations**:
- **Payout per win**: `betPerLine × paytableMultiplier`
- **Total win**: Sum of all payline wins
- **Max win cap**: `bet × maxWinMultiplier` (500x from config)
- **Final win**: `min(totalWin, maxWin)`

**Precision**: All calculations use `decimal.Round()` with 2 decimal places (MidpointRounding.ToEven)

### 6. Random Number Generation

**Primary**: External RNG service via `IRngClient` (for regulatory compliance)

**Fallback**: `FortunaPrng` (cryptographically secure PRNG)

**Seeds Used**:
- **Reel start positions**: One seed per reel (5 seeds)
- Each seed is converted to a reel stop position: `stopPosition = Math.Abs(seed) % reelLength`

**RNG Request** (`SpinHandler.FetchRandomContext()`, lines 689-706):
- Creates pool for "reel-starts" (5 seeds, one per reel)
- Includes metadata (reel lengths)
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

