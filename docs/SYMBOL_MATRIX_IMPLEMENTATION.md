# Symbol Matrix Implementation Plan

## Overview
This document outlines the implementation of symbol matrix (numeric IDs) instead of symbol names for GLI-19 compliance and API standardization.

## Current State
- Backend returns symbol codes as strings: `["RED", "BLUE", "WILD", ...]`
- Frontend maps symbol codes to display symbols
- Symbol catalog defines: `Sym`, `Code`, `DisplayName`, `Type`

## Target State
- Backend returns numeric symbol IDs: `[4, 6, 1, ...]` (0-based index in symbol catalog)
- Frontend receives numeric IDs and maps to display symbols
- Symbol mapping is verifiable for GLI-19 compliance

## Implementation Steps

### Step 1: Backend Changes
1. Create symbol ID mapping service
2. Update `FlattenCodes()` to return numeric IDs
3. Update `ResultsEnvelope` to use `IReadOnlyList<int>` instead of `IReadOnlyList<string>`
4. Ensure symbol catalog order is stable (used as ID mapping)

### Step 2: Frontend Changes
1. Create symbol ID to symbol code mapping
2. Update `engineResultConverter.ts` to handle numeric IDs
3. Map numeric IDs to frontend `SymbolId` types
4. Ensure backward compatibility during transition

### Step 3: API Compliance
1. Verify request/response structures match Client-RGS spec
2. Verify RGS-Game Engine spec compliance
3. Ensure RNG requests include roundId/gameId (already done)

### Step 4: GLI-19 Compliance
1. RNG requests include roundId and gameId ✅ (already implemented)
2. Symbol mapping is verifiable (symbol catalog order = ID mapping)
3. Transaction IDs tracked ✅ (already implemented)


