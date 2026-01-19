# Balance Endpoint Testing Guide

## Endpoint Details

**URL:** `POST /{operatorId}/player/balance`

**Example:** `POST http://localhost:5062/TEST/player/balance`

## Request Format

```json
{
  "playerId": "8bb5289dc906b0420c0d977480acc38d"
}
```

## Response Format

```json
{
  "statusCode": 8000,
  "message": "Request processed successfully",
  "balance": 10000.00
}
```

## Testing Methods

### 1. Using HTTP File (REST Client in VS Code)

The balance endpoint is already added to `RGS.http`:

```http
### Balance Request
POST http://localhost:5062/TEST/player/balance
Content-Type: application/json

{
  "playerId": "8bb5289dc906b0420c0d977480acc38d"
}
```

**To test:**
1. Open `backend/RGS/RGS/RGS.http` in VS Code
2. Install "REST Client" extension if not already installed
3. Click "Send Request" above the balance request section

### 2. Using cURL

```bash
curl -X POST http://localhost:5062/TEST/player/balance \
  -H "Content-Type: application/json" \
  -d '{
    "playerId": "8bb5289dc906b0420c0d977480acc38d"
  }'
```

### 3. Using Postman

1. Create new POST request
2. URL: `http://localhost:5062/TEST/player/balance`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
```json
{
  "playerId": "8bb5289dc906b0420c0d977480acc38d"
}
```

### 4. Using Swagger UI

1. Start the RGS service
2. Navigate to: `http://localhost:5062/swagger`
3. Find the `/{operatorId}/player/balance` endpoint
4. Click "Try it out"
5. Enter operatorId: `TEST`
6. Enter request body:
```json
{
  "playerId": "8bb5289dc906b0420c0d977480acc38d"
}
```
7. Click "Execute"

## Expected Response

**Success (200 OK):**
```json
{
  "statusCode": 8000,
  "message": "Request processed successfully",
  "balance": 10000.00
}
```

**Error - Missing playerId (200 OK with error code):**
```json
{
  "statusCode": 6001,
  "message": "playerId is required."
}
```

**Error - Null request (200 OK with error code):**
```json
{
  "statusCode": 6001,
  "message": "Bad request"
}
```

## Notes

- **Status Code 8000**: Used specifically for balance requests (per RGS spec)
- **Status Code 6001**: Bad request errors
- The endpoint currently returns a demo balance (10000.00) - in production, this should query the player service/database
- The `playerId` should match the token used in the `/start` endpoint

## Integration with Game Flow

The balance endpoint is typically called:
1. **Before game start** - To get initial balance
2. **Periodically** - To refresh balance display
3. **After game play** - To verify balance updates

## Current Implementation

- ✅ Endpoint exists at `/{operatorId}/player/balance`
- ✅ Accepts `playerId` in request body
- ✅ Returns status code 8000 (per spec)
- ⚠️ Currently returns hardcoded balance (10000.00)
- ⚠️ TODO: Integrate with player service/database for real balance
