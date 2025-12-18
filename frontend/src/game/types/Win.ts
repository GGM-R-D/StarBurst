/**
 * Win type definition for frontend display.
 * Wins come from backend - no local calculation.
 */
export interface Win {
  lineId: number;
  paylineIndex: number;
  symbol: string;
  count: number;
  payout: number;
  positions: Array<{ reel: number; row: number }>;
}

