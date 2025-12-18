# Game Engine API Request Examples

## Endpoint

**POST** `/play`

## Request Structure

The Game Engine expects a `PlayRequest` with the following structure:

```json
{
  "gameId": "string",
  "playerToken": "string",
  "bets": [
    {
      "betType": "string",
      "amount": 0.00
    }
  ],
  "baseBet": 0.00,
  "totalBet": 0.00,
  "betMode": "Standard" | "Ante",
  "isFeatureBuy": false,
  "engineState": null | {
    "freeSpins": null | {
      "spinsRemaining": 0,
      "totalSpinsAwarded": 0,
      "totalMultiplier": 0.00,
      "featureWin": 0.00,
      "justTriggered": false
    }
  },
  "userPayload": null,
  "lastResponse": null
}
```

## Example Requests

### Example 1: Base Game Spin (First Spin) - CORRECT FORMAT

```json
{
  "gameId": "starburst",
  "playerToken": "player-session-123",
  "bets": [
    {
      "betType": "BASE",
      "amount": 1.00
    }
  ],
  "baseBet": 1.00,
  "totalBet": 1.00,
  "betMode": "Standard",
  "isFeatureBuy": false,
  "engineState": null,
  "userPayload": null,
  "lastResponse": null
}
```

### Example 2: Base Game Spin (Subsequent Spin)

```json
{
  "gameId": "starburst",
  "playerToken": "player-session-123",
  "bets": [
    {
      "betType": "BASE",
      "amount": 1.00
    }
  ],
  "baseBet": 1.00,
  "totalBet": 1.00,
  "betMode": "Standard",
  "isFeatureBuy": false,
  "engineState": {},
  "userPayload": null,
  "lastResponse": null
}
```

### Example 3: Free Spins Spin

```json
{
  "gameId": "starburst",
  "playerToken": "player-session-123",
  "bets": [
    {
      "betType": "BASE",
      "amount": 1.00
    }
  ],
  "baseBet": 1.00,
  "totalBet": 1.00,
  "betMode": "Standard",
  "isFeatureBuy": false,
  "engineState": {
    "freeSpins": {
      "spinsRemaining": 10,
      "totalSpinsAwarded": 10,
      "totalMultiplier": 1.00,
      "featureWin": 0.00,
      "justTriggered": false
    }
  },
  "userPayload": null,
  "lastResponse": null
}
```

### Example 4: Feature Buy

```json
{
  "gameId": "starburst",
  "playerToken": "player-session-123",
  "bets": [
    {
      "betType": "BASE",
      "amount": 1.00
    }
  ],
  "baseBet": 1.00,
  "totalBet": 1.00,
  "betMode": "Standard",
  "isFeatureBuy": true,
  "engineState": {},
  "userPayload": null,
  "lastResponse": null
}
```

## Field Descriptions

### Required Fields

- **gameId** (string): The game identifier (e.g., "starburst", "junglerelics")
- **playerToken** (string): Unique player session/token identifier
- **bets** (array): Array of bet objects, each with:
  - **betType** (string): Type of bet (e.g., "BASE")
  - **amount** (decimal): Bet amount (e.g., 1.00, 5.50)
- **baseBet** (decimal): Base bet amount (must match bet amount)
- **totalBet** (decimal): Total bet amount (usually same as baseBet for standard mode)
- **betMode** (enum): Either "Standard" or "Ante"
- **isFeatureBuy** (boolean): Whether this is a feature buy request

### Optional Fields

- **engineState** (object | null): Current game state, can be:
  - `null` for first spin
  - Empty object `{}` for base game continuation
  - Object with `freeSpins` for free spins mode
- **userPayload** (object | null): Additional user data (optional, can be `null`)
- **lastResponse** (object | null): Previous response data (optional, can be `null`)

### EngineState Structure

When `engineState` is provided and contains free spins:

```json
{
  "freeSpins": {
    "spinsRemaining": 10,        // Number of free spins left
    "totalSpinsAwarded": 10,      // Total free spins awarded
    "totalMultiplier": 1.00,      // Cumulative multiplier
    "featureWin": 0.00,           // Total feature win so far
    "justTriggered": false         // Whether free spins just started
  }
}
```

## Common Errors and Solutions

### Error: "The request field is required"
- **Cause**: Request body is not being sent correctly or Content-Type header is missing
- **Solution**: Ensure `Content-Type: application/json` header is set and body is valid JSON

### Error: "is an invalid start of a property name" on engineState
- **Cause**: Using placeholder text like `{ ... }` instead of valid JSON
- **Solution**: Use `null` or a proper object structure (see examples above)

### Error: Invalid betMode value
- **Cause**: Using lowercase "standard" instead of "Standard"
- **Solution**: Use exact enum values: "Standard" or "Ante" (case-sensitive)

## cURL Examples

### Base Game Spin (First Spin)

```bash
curl -X POST http://localhost:8080/play \
  -H "Content-Type: application/json" \
  -H "accept: application/json" \
  -d '{
    "gameId": "starburst",
    "playerToken": "player-session-123",
    "bets": [
      {
        "betType": "BASE",
        "amount": 1.00
      }
    ],
    "baseBet": 1.00,
    "totalBet": 1.00,
    "betMode": "Standard",
    "isFeatureBuy": false,
    "engineState": null,
    "userPayload": null,
    "lastResponse": null
  }'
```

### Base Game Spin (Subsequent Spin)

```bash
curl -X POST http://localhost:8080/play \
  -H "Content-Type: application/json" \
  -H "accept: application/json" \
  -d '{
    "gameId": "starburst",
    "playerToken": "player-session-123",
    "bets": [
      {
        "betType": "BASE",
        "amount": 1.00
      }
    ],
    "baseBet": 1.00,
    "totalBet": 1.00,
    "betMode": "Standard",
    "isFeatureBuy": false,
    "engineState": {},
    "userPayload": null,
    "lastResponse": null
  }'
```

### Free Spins Spin

```bash
curl -X POST http://localhost:8080/play \
  -H "Content-Type: application/json" \
  -H "accept: application/json" \
  -d '{
    "gameId": "starburst",
    "playerToken": "player-session-123",
    "bets": [
      {
        "betType": "BASE",
        "amount": 1.00
      }
    ],
    "baseBet": 1.00,
    "totalBet": 1.00,
    "betMode": "Standard",
    "isFeatureBuy": false,
    "engineState": {
      "freeSpins": {
        "spinsRemaining": 10,
        "totalSpinsAwarded": 10,
        "totalMultiplier": 1.00,
        "featureWin": 0.00,
        "justTriggered": false
      }
    },
    "userPayload": null,
    "lastResponse": null
  }'
```

## Response Structure

The Game Engine returns a `PlayResponse`:

```json
{
  "statusCode": 200,
  "win": 0.00,
  "scatterWin": 0.00,
  "featureWin": 0.00,
  "buyCost": 0.00,
  "freeSpinsAwarded": 0,
  "roundId": "string",
  "timestamp": "2024-01-01T00:00:00Z",
  "nextState": {
    "freeSpins": null
  },
  "results": {
    "cascades": [],
    "wins": [],
    "scatter": null,
    "freeSpins": null,
    "rngTransactionId": "string",
    "finalGridSymbols": ["SYM1", "SYM2", ...]
  }
}
```

## Notes

- **Money values** are decimal numbers with up to 2 decimal places (e.g., `1.00`, `5.50`, `10.00`)
- **Bet amounts** must match between `bets[0].amount`, `baseBet`, and `totalBet` for standard mode
- **Game IDs** must match a configuration file in `/app/configs/` (e.g., `starburst.json`)
- **EngineState** should be `null` for the first spin, then use the `nextState` from the previous response
- The **finalGridSymbols** in the response contains the final reel positions as a flat array (row-major order)
- **Important**: Do NOT use placeholder text like `{ ... }` in JSON - use `null` or proper object structures
