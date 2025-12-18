export type SymbolId =
  | 'SYM_WILD'
  | 'SYM_BAR'
  | 'SYM_SEVEN'
  | 'SYM_ORANGE'
  | 'SYM_GREEN'
  | 'SYM_RED'
  | 'SYM_BLUE'
  | 'SYM_PURPLE';

// symbols[col][row] – column-major [0..cols-1][0..rows-1]
export interface SpinResult {
  symbols: SymbolId[][];
}


