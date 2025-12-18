# Starburst Standardization - Implementation Status

## ✅ Completed Changes

### Frontend
1. **Symbol Configuration** (`frontend/src/game/config/symbolConfig.ts`)
   - ✅ Fixed `appearsOn` to use 0-indexed reels: `[1, 2, 3]` (reels 2-4)
   - ✅ Confirmed only 8 symbols: SYM_WILD, SYM_BAR, SYM_SEVEN, SYM_ORANGE, SYM_GREEN, SYM_RED, SYM_BLUE, SYM_PURPLE
   - ✅ SYM_WILD configured as expanding wild, appears only on reels 2-4

2. **Demo Spin Provider** (`frontend/src/game/demo/DemoSpinDataProvider.ts`)
   - ✅ Enforces SYM_WILD only on reels 2-4 (indices 1, 2, 3)
   - ✅ Reels 0 and 4 (reels 1 and 5) never contain SYM_WILD

3. **ReelsView** (`frontend/src/game/reels/ReelsView.ts`)
   - ✅ Safety check prevents expansion on reels 0 and 4
   - ✅ Only reels 1, 2, 3 (reels 2-4) can be expanded

### Backend
1. **Paytable** (`backend/GameEngineHost/configs/starburst.json`)
   - ✅ Updated to Starburst math:
     - BAR: 3→10, 4→25, 5→50
     - SEVEN: 3→5, 4→12, 5→25
     - ORANGE: 3→2, 4→5, 5→12
     - GREEN: 3→1.6, 4→4, 5→10
     - RED: 3→1.4, 4→3, 5→8
     - BLUE: 3→1, 4→2, 5→5
     - PURPLE: 3→1, 4→2, 5→5
     - WILD: No direct payout (substitute only)

2. **Reel Strips** (`backend/GameEngineHost/configs/starburstReelsets.json`)
   - ✅ Removed Sym1 (WILD) from reels 0 and 4 in all reelsets:
     - `reelsetHigh`: Reels 0 and 4 exclude Sym1
     - `reelsetLow`: Reels 0 and 4 exclude Sym1
     - `reelsetBB`: Reels 0 and 4 exclude Sym1
     - `reelsetFreeSpins`: Reels 0 and 4 exclude Sym1
   - ✅ Reels 1, 2, 3 (indices 1, 2, 3) can contain Sym1 (WILD)

## ⚠️ Known Issues / TODO

### Backend WinEvaluator
**CRITICAL**: The backend `WinEvaluator.cs` currently has incorrect logic:
- Line 26: `if (symbolCount < 8)` - This prevents ANY payouts since you can't have 8+ of the same symbol on a 5x3 grid
- The evaluator doesn't use paylines - it just counts symbols across the entire grid
- Starburst requires 10 fixed paylines with left-to-right and right-to-left evaluation

**Required Fix**: The `WinEvaluator.cs` needs to be rewritten to:
1. Use the 10 Starburst paylines (defined in `frontend/src/game/config/paylines.ts`)
2. Evaluate wins along each payline (both directions)
3. Pay the highest win per payline
4. Sum wins across different paylines

### Paylines Configuration
The backend needs a paylines configuration matching:
```json
{
  "paylines": [
    { "id": 1, "rows": [1,1,1,1,1] },
    { "id": 2, "rows": [0,0,0,0,0] },
    { "id": 3, "rows": [2,2,2,2,2] },
    { "id": 4, "rows": [0,1,2,1,0] },
    { "id": 5, "rows": [2,1,0,1,2] },
    { "id": 6, "rows": [0,0,1,0,0] },
    { "id": 7, "rows": [2,2,1,2,2] },
    { "id": 8, "rows": [1,2,2,2,1] },
    { "id": 9, "rows": [1,0,0,0,1] },
    { "id": 10, "rows": [1,0,1,0,1] }
  ]
}
```

### Wild Expansion Logic
The backend expanding wild and respin logic needs verification:
- Ensure SYM_WILD expansion only triggers on reels 2-4 (indices 1, 2, 3)
- Verify respin logic matches frontend behavior
- Confirm maximum 3 expanded wild reels (max 3 respins)

## Summary

**Frontend**: ✅ Fully compliant with Starburst rules
- Wild restrictions enforced
- Paytable matches Starburst math
- Paylines correctly defined

**Backend**: ⚠️ Partial compliance
- ✅ Paytable updated to Starburst math
- ✅ Reel strips enforce wild restrictions
- ❌ WinEvaluator needs major refactor to use paylines
- ❌ Paylines configuration missing
- ⚠️ Wild expansion/respin logic needs verification

