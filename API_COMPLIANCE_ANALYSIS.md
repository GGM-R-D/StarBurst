# API Compliance Analysis - Requirements vs Implementation

## Document Review Summary

### 1. RGS-_Game server (math) example.txt (RGS → Game Engine API)

#### ✅ COMPLIANT Requirements:
- ✅ POST `/play` endpoint implemented
- ✅ `bets` array with `amount` property
- ✅ `userPayload` (Any type) - supported
- ✅ `lastResponse` (Object) - supported
- ✅ `statusCode` (Int) - returns 200
- ✅ `win` (Money) - returned
- ✅ `freeSpins` (Int) - returned as `FreeSpinsAwarded`
- ✅ `results` (Object) - returned as `ResultsEnvelope`
- ✅ Fortuna RNG algorithm used
- ✅ Jurisdiction enforced RNG with `roundId` and `gameId`

#### ❌ MISSING Requirements:
1. **`bet` (Money)** - Document says: "Contains the calculated total bet as sum of all the amount properties specified into the bets array. (IMPORTANT!)"
   - **Current**: We have `baseBet` and `totalBet` but not `bet`
   - **Issue**: The document specifies `bet` as a separate field, not `baseBet`/`totalBet`

2. **`message` (String)** - Document says: "A string message that describes the status code."
   - **Current**: `PlayResponse` doesn't have a `message` field
   - **Issue**: Response should include a message describing the status

3. **`rtpLevel` (Int)** - Document says: "If the game supports multiple RTP, in this field will be sent the level (1,2,3,4 etc.)"
   - **Current**: Not in `PlayRequest`
   - **Issue**: Optional but should be supported if game has multiple RTP levels

4. **`mode` (Int)** - Document says: "Identifies in which mode is playing the game. 0: normal, 1: in-game free spin, 2: bonus game, 3: free bets"
   - **Current**: We determine mode from `engineState` but don't receive it explicitly
   - **Issue**: Should accept `mode` parameter from RGS

5. **`currency` (Object)** - Document says: "It is a JSON object that contains information about the currency. Example: { 'id': 'EUR' }"
   - **Current**: Not in `PlayRequest`
   - **Issue**: Should accept currency information

6. **`feature` (Object)** - Document says: "If the game triggers a game feature such as a bonus game or a gamble game, the game engine must send back this object with two mandatory properties (type and isClosure) and one optional (name)"
   - **Current**: `PlayResponse` doesn't have a `feature` object
   - **Issue**: Should return feature information when triggered

### 2. Game server -_ RNG example.txt (Game Engine → RNG API)

#### ✅ COMPLIANT:
- ✅ Uses Fortuna algorithm
- ✅ Jurisdiction enforced pools request includes `roundId` and `gameId`
- ✅ Uses `/pools` endpoint
- ✅ Proper pool structure with metadata

### 3. Client-_RGS example.txt (Client → RGS API)

#### ✅ COMPLIANT:
- ✅ POST `/{operatorId}/{gameId}/start` endpoint
- ✅ POST `/{operatorId}/{gameId}/play` endpoint
- ✅ POST `/{operatorId}/player/balance` endpoint
- ✅ Response structure matches specification
- ✅ `game.results` contains engine response (wrapped correctly)

#### ⚠️ NOTES:
- The RGS wraps engine response in an envelope (which we handle correctly)
- Response structure matches the spec

## Required Changes

### Priority 1: Critical Missing Fields

1. **Add `bet` field to PlayRequest** (if RGS sends it, or calculate from bets array)
2. **Add `message` field to PlayResponse**
3. **Add `feature` object to PlayResponse** (when feature is triggered)

### Priority 2: Optional but Recommended

4. **Add `rtpLevel` to PlayRequest** (optional)
5. **Add `mode` to PlayRequest** (optional, but helps with state management)
6. **Add `currency` to PlayRequest** (optional)

## Implementation Plan

1. Update `PlayRequest` to include missing fields
2. Update `PlayResponse` to include `message` and `feature`
3. Update RGS → Game Engine transformation to map fields correctly
4. Ensure feature detection and reporting works correctly

