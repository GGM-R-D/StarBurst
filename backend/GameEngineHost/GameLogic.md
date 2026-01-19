# Starburst Game Logic Documentation

## Overview

Starburst is a **non-cascading slot game** with a **5×3 grid** (5 reels, 3 rows) featuring an **expanding wild respin feature**. The game uses **10 fixed paylines** with **bidirectional evaluation** (left-to-right and right-to-left).

**Key Characteristics:**
- **No cascading wins** - Symbols do not fall or cascade
- **No free spins feature** - Only expanding wild respin feature
- **Single win evaluation** - Wins are evaluated once per spin
- **Expanding wilds** - Wilds on reels 2, 3, 4 expand to fill entire reel and trigger respins

---

## Game Flow

### 1. Spin Request Processing

**Entry Point:** `SpinHandler.PlayAsync()`

**Process:**
1. Validate request (game ID, bet amounts, etc.)
2. Load game configuration from JSON
3. Generate round ID
4. Clone engine state (or create new if first spin)
5. **Clear invalid respin state** if respins exhausted or new session
6. Determine spin mode:
   - `BaseGame` - Normal spin
   - `Respin` - Respin feature active
   - `BuyEntry` - Feature buy

### 2. Reel Strip Selection

**Location:** `SpinHandler.SelectReelStrips()`

**Selection Logic:**
- **Respin Mode** → Uses `reelsetFreeSpins` (special reel set for respins)
- **Buy Entry Mode** → Uses `reelsetBB` (buy bonus reel set)
- **Base Game** → Weighted selection between `reelsetHigh` and `reelsetLow`
  - Weights from config: `betModes.standard.reelWeights` (e.g., low: 70%, high: 30%)
  - Random selection based on weights to control RTP

### 3. Random Number Generation

**Location:** `SpinHandler.FetchRandomContext()`

**RNG Seeds Required:**
- **5 seeds** for reel start positions (one per reel)
- **15 seeds** for multiplier assignment (one per grid position, if multipliers enabled)

**RNG Service:**
- Primary: External RNG service via `IRngClient` (for regulatory compliance)
- Fallback: `FortunaPrng` (cryptographically secure PRNG)

### 4. Board Creation

**Location:** `ReelBoard.Create()`

**Process:**
1. For each of 5 reels:
   - Get reel strip (array of symbols)
   - Use RNG seed to determine start position: `startIndex = seed % reelLength`
   - Extract 3 consecutive symbols starting from `startIndex` (wraps around if needed)
   - Create column with 3 symbols (top to bottom)
2. **Special handling for respins:**
   - Locked wild reels (from previous respin) are **preserved** - they don't spin
   - Only non-locked reels spin during respins
3. Result: 5 columns × 3 rows grid

**Grid Format:**
- Flattened as: `[row2col0, row2col1, ..., row2col4, row1col0, ..., row0col0, ...]`
- Bottom-to-top order: Row 2 (bottom) → Row 1 (middle) → Row 0 (top)

### 5. Initial Grid Capture

**Location:** `SpinHandler.PlayAsync()` (line 137)

**Purpose:** Capture grid state **before wild expansion** for frontend animation

**Process:**
- Flatten board codes to array
- Map symbol codes to numeric IDs
- Store as `initialGrid` in feature response

### 6. Wild Detection

**Location:** `SpinHandler.DetectWildReels()`

**Rules:**
- Wilds can **ONLY** appear on **reels 2, 3, 4** (0-based indices: 1, 2, 3)
- Reels 1 and 5 (indices 0 and 4) **NEVER** have wilds
- Detects wilds from **initial board state** (before expansion)

**Process:**
1. Check each reel (2, 3, 4) for wild symbols
2. Return list of reel indices that contain wilds

### 7. Expanding Wild Information Capture

**Location:** `SpinHandler.DetectExpandingWilds()`

**Purpose:** Capture which rows contain wilds **before expansion**

**Process:**
- For each wild reel, record which rows (0, 1, or 2) contain wild symbols
- Used in feature response to show original wild positions

**Example:**
- Reel 3 has wild at row 1 (middle) → `ExpandingWildInfo(Reel: 3, Rows: [1])`
- After expansion → `ExpandingWildInfo(Reel: 3, Rows: [0, 1, 2])`

### 8. Wild Expansion

**Location:** `SpinHandler.PlayAsync()` and `ReelBoard.ExpandReelToWild()`

**Expansion Rules:**
- Wilds expand to fill **entire reel** (all 3 rows)
- Expansion happens **BEFORE win evaluation** (so expanded wilds can substitute)
- Only reels 2, 3, 4 can be expanded

**Expansion Timing:**

**Base Game / Buy Entry:**
- If wilds detected → Expand immediately (before win evaluation)
- Trigger respin feature if wilds found

**Respin Mode:**
- **Step 1:** Verify locked reels are still wild (safety check)
- **Step 2:** Detect **new wilds** on non-locked reels
- **Step 3:** Expand new wilds immediately (before win evaluation)
- **Step 4:** Award additional respins for new wilds

### 9. Win Evaluation

**Location:** `WinEvaluator.Evaluate()`

**Important:** Starburst is **NOT a cascading game** - only **one evaluation** per spin.

#### 10 Fixed Paylines

```csharp
Payline 1: [1, 1, 1, 1, 1]  // Middle row (straight)
Payline 2: [0, 0, 0, 0, 0]  // Top row (straight)
Payline 3: [2, 2, 2, 2, 2]  // Bottom row (straight)
Payline 4: [0, 1, 2, 1, 0]  // V-shape up
Payline 5: [2, 1, 0, 1, 2]  // V-shape down (inverted V)
Payline 6: [0, 0, 1, 0, 0]  // Top-center
Payline 7: [2, 2, 1, 2, 2]  // Bottom-center
Payline 8: [1, 2, 2, 2, 1]  // Bottom-heavy
Payline 9: [1, 0, 0, 0, 1]  // Top-heavy
Payline 10: [1, 0, 1, 0, 1] // Alternating
```

**Row Indices:** 0 = top, 1 = middle, 2 = bottom

#### Evaluation Process

1. **Bet per line** = Total bet ÷ 10 paylines

2. **For each payline:**
   - Extract symbols along payline path
   - Evaluate **left-to-right** direction
   - Evaluate **right-to-left** direction
   - Choose the **best win** from both directions

3. **Wild Substitution:**
   - WILD can substitute for any symbol
   - Base symbol = first non-wild symbol (from start direction)
   - If all wilds → no win (wilds don't pay directly)

4. **Match Counting:**
   - Count consecutive matching symbols (including wilds)
   - Stop at first non-matching symbol
   - Minimum 3 symbols required for win

5. **Paytable Lookup:**
   - Match count against paytable
   - Find best multiplier for count (e.g., 4 of a kind uses 4x multiplier, not 3x)

6. **Payout Calculation:**
   - `payout = betPerLine × paytableMultiplier`

7. **Highest Win Per Payline:**
   - Only the **highest win** on each payline is paid
   - If both directions have wins, choose better one

#### Paytable (from `starburst.json`)

| Symbol | 3 of a kind | 4 of a kind | 5 of a kind |
|--------|-------------|-------------|-------------|
| BAR    | 50x         | 200x        | 250x        |
| SEVEN  | 25x         | 60x         | 120x        |
| ORANGE | 10x         | 25x         | 60x         |
| GREEN  | 8x          | 20x         | 50x         |
| RED    | 7x          | 15x         | 40x         |
| BLUE   | 5x          | 10x         | 25x         |
| PURPLE | 5x          | 10x         | 25x         |
| WILD   | No payout   | No payout   | No payout   |

**Note:** WILD symbols do not pay directly - they only substitute for other symbols.

### 10. Multiplier Application

**Location:** `SpinHandler.PlayAsync()` (line 267)

**Rules:**
- Multipliers only apply in **Base Game** and **Buy Entry** modes
- **Not used in Starburst** (no multiplier symbols configured)
- If multipliers present: `finalWin = baseWin × multiplierSum`

### 11. Respin Feature Management

**Location:** `SpinHandler.HandleWildRespinFeature()`

#### Feature Trigger (Base Game)

**When:** Wilds detected on reels 2, 3, or 4 during base game spin

**Process:**
1. Wilds are expanded (before win evaluation)
2. Initialize respin state:
   - `RespinsRemaining = min(wildReels.Count, 3)` (max 3 respins)
   - `LockedWildReels = wildReels` (reels with wilds)
   - `TotalRespinsAwarded = RespinsRemaining`
   - `JustTriggered = true`

**Important:** The **triggering spin is NOT a respin** - it's a base game spin. Respins happen on **subsequent requests**.

#### Respin Execution

**When:** `RespinsRemaining > 0` in engine state

**Process:**
1. **Board Creation:**
   - Locked wild reels are **preserved** (don't spin)
   - Only non-locked reels spin

2. **Wild Detection:**
   - Detect **new wilds** on non-locked reels
   - New wilds must be on reels 2, 3, 4

3. **Wild Expansion:**
   - Expand new wilds immediately (before win evaluation)

4. **Additional Respins:**
   - Each new wild reel awards **1 additional respin**
   - Maximum **3 total respins** (one per wild reel)
   - New wild reels are added to `LockedWildReels`

5. **Respin Completion:**
   - Decrement `RespinsRemaining` by 1
   - Set `JustTriggered = false`

#### Feature End

**When:** `RespinsRemaining` reaches 0

**Process:**
- Respin state is kept with `RespinsRemaining = 0` for **one response** to signal feature closure
- On next base game spin, respin state is cleared

#### Maximum Respins

- **Maximum 3 respins total** (one per wild reel)
- Maximum 3 locked wild reels (reels 2, 3, 4)
- If 3 wild reels already locked, no additional respins awarded

### 12. Max Win Cap

**Location:** `SpinHandler.PlayAsync()` (line 338)

**Rule:**
- Maximum win = `bet × maxWinMultiplier` (500x from config)
- If `totalWin > maxWin` → `totalWin = maxWin`

### 13. Response Creation

**Location:** `SpinHandler.PlayAsync()` (line 439)

**Response Structure:**
- `PlayResponse` with:
  - Win amounts (total, scatter, feature)
  - Round ID and timestamp
  - `NextState` (engine state for next request)
  - `Results` (cascades, wins, final grid)
  - `Feature` (feature outcome if feature active)

**Cascade Step:**
- Single cascade step created (for compatibility)
- `GridBefore` = initial grid (before wild expansion)
- `GridAfter` = final grid (after wild expansion)
- Allows frontend to animate wild expansion

**Feature Outcome:**
- Type: `"EXPANDING_WILDS"`
- Includes:
  - `Active` - Whether feature is active
  - `RespinsRemaining` - Respins left
  - `LockedReels` - Locked reel indices
  - `ExpandingWilds` - Wild reel and row information
  - `InitialGrid` - Grid before expansion (for animation)

---

## State Management

### Engine Session State

**Location:** `EngineState.cs`

**Structure:**
```csharp
EngineSessionState
├── FreeSpins (null in Starburst - no free spins)
└── Respins
    ├── RespinsRemaining (0-3)
    ├── LockedWildReels (HashSet<int>)
    ├── TotalRespinsAwarded
    └── JustTriggered
```

**State Flow:**
1. **Base Game** → No respin state
2. **Wild Detected** → Create respin state, `RespinsRemaining = 1-3`
3. **Respin** → Decrement `RespinsRemaining`, detect new wilds
4. **Feature End** → `RespinsRemaining = 0`, cleared on next base game

**State Persistence:**
- State is passed in `PlayRequest.EngineState`
- Updated state returned in `PlayResponse.NextState`
- RGS stores state in session and passes it back on next request

---

## Special Rules

### 1. Wild Reel Restrictions

- **Wilds can ONLY appear on reels 2, 3, 4** (indices 1, 2, 3)
- Reels 1 and 5 (indices 0 and 4) **NEVER** have wilds
- Enforced during board creation and wild expansion

### 2. No Cascading

- **Starburst is NOT a cascading game**
- Symbols do not fall or cascade
- Only **one win evaluation** per spin
- No symbol removal or refill

### 3. No Free Spins

- Starburst has **no free spins feature**
- Only expanding wild respin feature exists
- `FreeSpins` state is always `null`

### 4. Bidirectional Paylines

- Each payline evaluated **both directions**
- Left-to-right and right-to-left
- Best win from either direction is paid

### 5. Single Win Per Payline

- Only the **highest win** on each payline is paid
- If multiple wins on same payline, only best one counts

---

## Money Calculations

### Precision

- All money uses `Money` struct with `decimal(20,2)` precision
- All calculations rounded to 2 decimal places (`MidpointRounding.ToEven`)

### Calculations

1. **Bet per line:** `betPerLine = totalBet ÷ 10`
2. **Payout per win:** `payout = betPerLine × paytableMultiplier`
3. **Total win:** `totalWin = sum(all payline wins)`
4. **Max win cap:** `maxWin = bet × 500`
5. **Final win:** `finalWin = min(totalWin, maxWin)`

---

## Configuration

### Game Configuration (`starburst.json`)

**Key Settings:**
- Board: 5 columns × 3 rows
- 8 symbols in catalog
- Paytable with multipliers for 3, 4, 5 of a kind
- Bet modes with reel weights
- Max win multiplier: 500x

### Reel Sets (`starburstReelsets.json`)

**Reel Sets:**
- `reelsetHigh` - High RTP configuration
- `reelsetLow` - Low RTP configuration
- `reelsetBB` - Buy bonus entry
- `reelsetFreeSpins` - Used for respin mode

**Selection:**
- Base game: Weighted selection (high/low)
- Respin: Always uses `reelsetFreeSpins`
- Buy entry: Always uses `reelsetBB`

---

## Request/Response Flow

### Request Structure

```json
{
  "gameId": "starburst",
  "playerToken": "session-id",
  "bets": [{"betType": "BASE", "amount": 1.00}],
  "baseBet": 1.00,
  "totalBet": 1.00,
  "betMode": "Standard",
  "isFeatureBuy": false,
  "engineState": {
    "respins": {
      "respinsRemaining": 1,
      "lockedWildReels": [2],
      "totalRespinsAwarded": 1,
      "justTriggered": false
    }
  }
}
```

### Response Structure

```json
{
  "statusCode": 200,
  "win": 4.00,
  "roundId": "ABC-123",
  "nextState": {
    "respins": {
      "respinsRemaining": 0,
      "lockedWildReels": [2, 3],
      "totalRespinsAwarded": 2
    }
  },
  "results": {
    "cascades": [{
      "gridBefore": [7, 5, 5, 3, 5, ...],  // Before expansion
      "gridAfter": [7, 5, 5, 0, 5, ...],   // After expansion
      "winsAfterCascade": [...],
      "totalWin": 4.00
    }],
    "finalGridSymbols": [7, 5, 5, 0, 5, ...]
  },
  "feature": {
    "type": "EXPANDING_WILDS",
    "active": false,
    "respinsRemaining": 0,
    "lockedReels": [2, 3],
    "expandingWilds": [
      {"reel": 2, "rows": [0, 1, 2]},
      {"reel": 3, "rows": [0, 1, 2]}
    ],
    "initialGrid": [7, 5, 5, 3, 5, ...]  // Before expansion
  }
}
```

---

## Summary

Starburst is a **simple, non-cascading slot game** with:

1. **5×3 grid** with 10 fixed paylines
2. **Bidirectional payline evaluation** (left-to-right and right-to-left)
3. **Expanding wild respin feature:**
   - Wilds on reels 2, 3, 4 expand to fill entire reel
   - Each wild reel awards 1 respin (max 3 respins)
   - Locked wild reels stay locked during respins
   - New wilds during respins award additional respins
4. **Single win evaluation** per spin (no cascades)
5. **No free spins feature** (only respin feature)
6. **Configuration-driven** (RTP, payouts, reel sets in JSON)

The game logic is centralized in `SpinHandler.PlayAsync()` with clear separation between board creation, wild expansion, win evaluation, and state management.
