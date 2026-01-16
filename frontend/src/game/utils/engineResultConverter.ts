import type { SymbolId } from '@game/demo/SpinTypes';
import type { PlayResponse } from '@network/types';

/**
 * Converts game engine results to frontend format.
 * The engine returns FinalGridSymbols as a flat array in row-major order (row by row, left to right).
 * The frontend expects a column-major grid: SymbolId[][], where [col][row].
 */
export function convertEngineResultsToGrid(
  engineResults: unknown,
  cols: number = 5,
  rows: number = 3
): SymbolId[][] | null {
  if (!engineResults || typeof engineResults !== 'object') {
    return null;
  }

  // Handle nested structure: RGS wraps ResultsEnvelope in an envelope with statusCode, message, etc.
  // Structure can be:
  // 1. Direct: { finalGridSymbols: [...], cascades: [...] }
  // 2. Nested: { statusCode: 200, message: "OK", results: { finalGridSymbols: [...], cascades: [...] } }
  const rawResults = engineResults as {
    statusCode?: number;
    message?: string;
    win?: number;
    freeSpins?: number;
    results?: {
      finalGridSymbols?: number[] | string[] | null; // Symbol matrix: numeric IDs or legacy string codes
      cascades?: Array<{
        gridAfter?: number[] | string[];
        gridBefore?: number[] | string[];
      }>;
    };
    // Also check top-level for direct structure
    finalGridSymbols?: number[] | string[] | null;
    cascades?: Array<{
      gridAfter?: number[] | string[];
      gridBefore?: number[] | string[];
    }>;
  };

  // Unwrap nested structure if present
  const results = rawResults.results && typeof rawResults.results === 'object'
    ? rawResults.results
    : rawResults;

  // Try to get final grid symbols (can be numeric IDs or string codes for backward compatibility)
  let symbolData: number[] | string[] | undefined;
  
  // Check for finalGridSymbols (camelCase from JSON serialization of FinalGridSymbols)
  // Also check PascalCase for direct C# object serialization
  if (results.finalGridSymbols && Array.isArray(results.finalGridSymbols) && results.finalGridSymbols.length > 0) {
    symbolData = results.finalGridSymbols;
  } else if ((results as any).FinalGridSymbols && Array.isArray((results as any).FinalGridSymbols) && (results as any).FinalGridSymbols.length > 0) {
    symbolData = (results as any).FinalGridSymbols;
  } else if (results.cascades && results.cascades.length > 0) {
    // Fallback to last cascade gridAfter
    const lastCascade = results.cascades[results.cascades.length - 1];
    if (lastCascade?.gridAfter && Array.isArray(lastCascade.gridAfter) && lastCascade.gridAfter.length > 0) {
      symbolData = lastCascade.gridAfter;
    } else if ((lastCascade as any)?.GridAfter && Array.isArray((lastCascade as any).GridAfter) && (lastCascade as any).GridAfter.length > 0) {
      symbolData = (lastCascade as any).GridAfter;
    }
  }
  
  // Debug logging
  if (!symbolData) {
    console.warn('No symbol data found in engine results', {
      hasFinalGridSymbols: !!results.finalGridSymbols,
      finalGridSymbolsLength: results.finalGridSymbols?.length,
      hasCascades: !!results.cascades,
      cascadesLength: results.cascades?.length,
      resultsKeys: Object.keys(results || {})
    });
  }

  if (!symbolData || symbolData.length !== cols * rows) {
    console.warn('Invalid engine results format or size mismatch', { symbolData, cols, rows });
    return null;
  }

  // Symbol matrix mapping: numeric ID -> SymbolId
  // Matches backend symbol catalog order: 0=WILD, 1=BAR, 2=SEVEN, 3=RED, 4=PURPLE, 5=BLUE, 6=GREEN, 7=ORANGE
  const symbolIdMap: SymbolId[] = [
    'SYM_WILD',    // 0
    'SYM_BAR',     // 1
    'SYM_SEVEN',   // 2
    'SYM_RED',     // 3
    'SYM_PURPLE',  // 4
    'SYM_BLUE',    // 5
    'SYM_GREEN',   // 6
    'SYM_ORANGE',  // 7
  ];

  // Legacy symbol code mapping (for backward compatibility during transition)
  const symbolCodeMap: Record<string, SymbolId> = {
    'SYM_WILD': 'SYM_WILD',
    'SYM_BAR': 'SYM_BAR',
    'SYM_SEVEN': 'SYM_SEVEN',
    'SYM_RED': 'SYM_RED',
    'SYM_PURPLE': 'SYM_PURPLE',
    'SYM_BLUE': 'SYM_BLUE',
    'SYM_GREEN': 'SYM_GREEN',
    'SYM_ORANGE': 'SYM_ORANGE',
    'WILD': 'SYM_WILD',
    'BAR': 'SYM_BAR',
    'SEVEN': 'SYM_SEVEN',
    'RED': 'SYM_RED',
    'PURPLE': 'SYM_PURPLE',
    'BLUE': 'SYM_BLUE',
    'GREEN': 'SYM_GREEN',
    'ORANGE': 'SYM_ORANGE',
  };

  // Helper to convert symbol data (ID or code) to SymbolId
  const toSymbolId = (value: number | string): SymbolId => {
    if (typeof value === 'number') {
      // Numeric ID: use symbol matrix
      if (value >= 0 && value < symbolIdMap.length) {
        return symbolIdMap[value];
      }
      console.warn(`Invalid symbol ID: ${value}, falling back to SYM_BAR`);
      return 'SYM_BAR';
    } else {
      // String code: use legacy mapping (backward compatibility)
      return symbolCodeMap[value] || 'SYM_BAR';
    }
  };

  // Engine returns symbols in row-major order, bottom-to-top: 
  // Backend FlattenIds: for (row = rows-1; row >= 0; row--) for each column
  // So format is: [BOT_ROW_ALL_COLS, MID_ROW_ALL_COLS, TOP_ROW_ALL_COLS]
  // For 3x5 grid: 
  //   - Backend row 2 (bottom) = indices 0-4 (first 5 elements)
  //   - Backend row 1 (middle) = indices 5-9 (next 5 elements)
  //   - Backend row 0 (top) = indices 10-14 (last 5 elements)
  // Frontend expects column-major: [[col0row0, col0row1, col0row2], [col1row0, ...], ...]
  // Where row0 is top, row2 is bottom
  const grid: SymbolId[][] = [];
  
  for (let col = 0; col < cols; col++) {
    grid[col] = [];
    for (let row = 0; row < rows; row++) {
      // Backend iterates bottom-to-top: row 2, then 1, then 0
      // So backend row 2 (bottom) is at flat array position 0, row 1 at position 1, row 0 at position 2
      // Formula: flatIndex = (rows - 1 - backendRow) * cols + col
      // But we want: frontendRow 0 (top) -> backendRow 0 (top), frontendRow 2 (bottom) -> backendRow 2 (bottom)
      // So: backendRow = frontendRow
      // flatIndex = (rows - 1 - frontendRow) * cols + col
      const backendRow = row; // Frontend row index matches backend row index (0=top, 2=bottom)
      const flatIndex = (rows - 1 - backendRow) * cols + col;
      const symbolValue = symbolData[flatIndex];
      const symbolId = toSymbolId(symbolValue);
      grid[col][row] = symbolId;
    }
  }

  return grid;
}

/**
 * Extracts win information from engine results.
 * Backend returns wins with grid indices, we need to convert to payline format.
 */
export function extractWinsFromEngineResults(
  engineResults: unknown,
  cols: number = 5,
  rows: number = 3
): Array<{
  lineId: number;
  paylineIndex: number;
  symbol: string;
  count: number;
  payout: number;
  positions: Array<{ reel: number; row: number }>;
}> {
  if (!engineResults || typeof engineResults !== 'object') {
    return [];
  }

  const results = engineResults as {
    wins?: Array<{
      symbolCode?: string;
      count?: number;
      multiplier?: number;
      payout?: { amount?: number } | number; // Can be object or direct number
      indices?: number[];
    }>;
  };

  if (!results.wins || !Array.isArray(results.wins)) {
    console.warn('[extractWinsFromEngineResults] No wins array found in results', {
      hasWins: !!results.wins,
      resultsKeys: Object.keys(results),
      resultsType: typeof results
    });
    return [];
  }
  
  console.info('[extractWinsFromEngineResults] Found wins:', {
    count: results.wins.length,
    sample: results.wins[0]
  });

  // Convert backend grid indices to reel/row positions
  // Backend flattens grid as: [row2col0, row2col1, ..., row2col4, row1col0, ..., row0col0, ...]
  // So for index i: row = rows - 1 - Math.floor(i / cols), col = i % cols
  const indexToPosition = (index: number): { reel: number; row: number } => {
    const flatRow = Math.floor(index / cols);
    const row = rows - 1 - flatRow; // Convert from bottom-to-top to top-to-bottom
    const reel = index % cols;
    return { reel, row };
  };

  // Map symbol codes to SymbolId format
  const symbolCodeMap: Record<string, string> = {
    'WILD': 'SYM_WILD',
    'BAR': 'SYM_BAR',
    'SEVEN': 'SYM_SEVEN',
    'RED': 'SYM_RED',
    'PURPLE': 'SYM_PURPLE',
    'BLUE': 'SYM_BLUE',
    'GREEN': 'SYM_GREEN',
    'ORANGE': 'SYM_ORANGE',
  };

  return results.wins
    .filter(win => {
      const hasRequired = win.symbolCode && win.count && win.payout && win.indices && win.indices.length > 0;
      if (!hasRequired) {
        console.warn('[extractWinsFromEngineResults] Skipping invalid win:', win);
      }
      return hasRequired;
    })
    .map((win, index) => {
      const positions = win.indices!.map(indexToPosition);
      const symbolCode = win.symbolCode!;
      const symbol = symbolCodeMap[symbolCode] || `SYM_${symbolCode}`;
      
      // Extract payout amount - handle both object and direct number
      let payoutAmount = 0;
      if (typeof win.payout === 'number') {
        payoutAmount = win.payout;
      } else if (win.payout && typeof win.payout === 'object' && 'amount' in win.payout) {
        payoutAmount = win.payout.amount ?? 0;
      }
      
      // Assign paylineIndex based on win index (backend doesn't track payline, so we use sequential)
      // In a real implementation, backend should include paylineIndex in SymbolWin
      const paylineIndex = index;
      
      return {
        lineId: paylineIndex + 1, // Line IDs start at 1
        paylineIndex,
        symbol,
        count: win.count!,
        payout: payoutAmount,
        positions,
      };
    });
}

