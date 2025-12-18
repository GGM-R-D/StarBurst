export const enum SymbolId {
  SYM_GEM_PURPLE = 1,
  SYM_GEM_BLUE = 2,
  SYM_GEM_GREEN = 3,
  SYM_GEM_RED = 4,
  SYM_GEM_YELLOW = 5,
  SYM_WILD = 99
}

export interface SymbolConfig {
  id: SymbolId;
  key: string;
  payout: number;
}

export const symbols: SymbolConfig[] = [
  { id: SymbolId.SYM_GEM_PURPLE, key: 'gem_purple', payout: 50 },
  { id: SymbolId.SYM_GEM_BLUE, key: 'gem_blue', payout: 40 },
  { id: SymbolId.SYM_GEM_GREEN, key: 'gem_green', payout: 30 },
  { id: SymbolId.SYM_GEM_RED, key: 'gem_red', payout: 20 },
  { id: SymbolId.SYM_GEM_YELLOW, key: 'gem_yellow', payout: 10 },
  { id: SymbolId.SYM_WILD, key: 'wild', payout: 0 }
];

