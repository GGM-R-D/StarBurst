/**
 * Centralized symbol configuration for Stellar Gems - Starburst-style rules.
 * This is the single source of truth for symbol definitions and paytable math.
 */

export interface SymbolConfig {
  id: string;
  type?: 'expanding_wild' | 'paying';
  appearsOn?: number[]; // Reel indices where this symbol can appear (0-indexed: 0-4)
  expands?: boolean;
  triggersRespin?: boolean;
  pays: Record<number, number>; // count -> multiplier (e.g., { 3: 10, 4: 25, 5: 50 })
}

export const SYMBOLS: Record<string, SymbolConfig> = {
  SYM_WILD: {
    id: 'SYM_WILD',
    type: 'expanding_wild',
    appearsOn: [1, 2, 3], // Reels 2-4 only (0-indexed: reels 1, 2, 3 = reels 2, 3, 4)
    expands: true,
    triggersRespin: true,
    pays: {} // Wild itself has no direct payout
  },

  SYM_BAR: {
    id: 'SYM_BAR',
    pays: { 3: 10, 4: 25, 5: 50 }
  },

  SYM_SEVEN: {
    id: 'SYM_SEVEN',
    pays: { 3: 5, 4: 12, 5: 25 }
  },

  SYM_ORANGE: {
    id: 'SYM_ORANGE',
    pays: { 3: 2, 4: 5, 5: 12 }
  },

  SYM_GREEN: {
    id: 'SYM_GREEN',
    pays: { 3: 1.6, 4: 4, 5: 10 }
  },

  SYM_RED: {
    id: 'SYM_RED',
    pays: { 3: 1.4, 4: 3, 5: 8 }
  },

  SYM_BLUE: {
    id: 'SYM_BLUE',
    pays: { 3: 1, 4: 2, 5: 5 }
  },

  SYM_PURPLE: {
    id: 'SYM_PURPLE',
    pays: { 3: 1, 4: 2, 5: 5 }
  }
};

/**
 * Get payout multiplier for a symbol and count.
 * Returns 0 if no payout exists for that count.
 */
export function getPayoutMultiplier(symbolId: string, count: number): number {
  const symbol = SYMBOLS[symbolId];
  if (!symbol || !symbol.pays) return 0;
  return symbol.pays[count] || 0;
}

/**
 * Check if a symbol is an expanding wild.
 */
export function isExpandingWild(symbolId: string): boolean {
  return SYMBOLS[symbolId]?.expands === true;
}

/**
 * Check if a symbol can appear on a specific reel (0-indexed: 0-4).
 */
export function canAppearOnReel(symbolId: string, reelIndex: number): boolean {
  const symbol = SYMBOLS[symbolId];
  if (!symbol || !symbol.appearsOn) return true; // Default: can appear anywhere
  return symbol.appearsOn.includes(reelIndex);
}

