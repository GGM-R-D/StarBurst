/**
 * Payline definitions for 5x3 slot game - Starburst 10-line layout.
 * Each payline has an id and a rows array where rows[i] is the row index (0-2) for reel i (0-4).
 * Row indices: 0 = top, 1 = middle, 2 = bottom
 */

export interface Payline {
  id: number;
  rows: number[]; // Row index for each reel (5 reels = 5 row indices)
}

export const PAYLINES: Payline[] = [
  // Payline 1: Middle row (straight across)
  { id: 1, rows: [1, 1, 1, 1, 1] },
  
  // Payline 2: Top row (straight across)
  { id: 2, rows: [0, 0, 0, 0, 0] },
  
  // Payline 3: Bottom row (straight across)
  { id: 3, rows: [2, 2, 2, 2, 2] },
  
  // Payline 4: V shape (top to bottom)
  { id: 4, rows: [0, 1, 2, 1, 0] },
  
  // Payline 5: Inverted V shape (bottom to top)
  { id: 5, rows: [2, 1, 0, 1, 2] },
  
  // Payline 6: Top-center pattern
  { id: 6, rows: [0, 0, 1, 0, 0] },
  
  // Payline 7: Bottom-center pattern
  { id: 7, rows: [2, 2, 1, 2, 2] },
  
  // Payline 8: Bottom-heavy pattern
  { id: 8, rows: [1, 2, 2, 2, 1] },
  
  // Payline 9: Top-heavy pattern
  { id: 9, rows: [1, 0, 0, 0, 1] },
  
  // Payline 10: Alternating top-middle pattern
  { id: 10, rows: [1, 0, 1, 0, 1] }
];

