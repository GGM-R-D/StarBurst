// src/game/config/paytableConfig.ts
import type { SymbolId } from '@game/demo/SpinTypes';

export interface SymbolPayEntry {
  id: SymbolId;
  displayName: string;
  pays: Record<number, number>; // key = count (3/4/5), value = payout (per 1x bet)
  isWild?: boolean;
  description?: string;
}

export const SYMBOL_PAYTABLE: SymbolPayEntry[] = [
  {
    id: 'SYM_WILD' as SymbolId,
    displayName: 'Wild',
    pays: {}, // no direct pays
    isWild: true,
    description: 'Wild appears only on reels 2, 3 and 4. It expands to cover the entire reel and awards respins. Expanded wilds remain locked during respins.'
  },
  {
    id: 'SYM_BAR' as SymbolId,
    displayName: 'Bar',
    pays: { 3: 10, 4: 25, 5: 50 }
  },
  {
    id: 'SYM_SEVEN' as SymbolId,
    displayName: 'Seven',
    pays: { 3: 5, 4: 12, 5: 25 }
  },
  {
    id: 'SYM_ORANGE' as SymbolId,
    displayName: 'Orange Gem',
    pays: { 3: 2, 4: 5, 5: 12 }
  },
  {
    id: 'SYM_GREEN' as SymbolId,
    displayName: 'Green Gem',
    pays: { 3: 1.6, 4: 4, 5: 10 }
  },
  {
    id: 'SYM_RED' as SymbolId,
    displayName: 'Red Gem',
    pays: { 3: 1.4, 4: 3, 5: 8 }
  },
  {
    id: 'SYM_BLUE' as SymbolId,
    displayName: 'Blue Gem',
    pays: { 3: 1, 4: 2, 5: 5 }
  },
  {
    id: 'SYM_PURPLE' as SymbolId,
    displayName: 'Purple Gem',
    pays: { 3: 1, 4: 2, 5: 5 }
  }
];

export interface PaylineDef {
  id: number;
  rows: number[]; // length=5, 0=top, 1=middle, 2=bottom
}

export const PAYLINES_CONFIG: PaylineDef[] = [
  { id: 1,  rows: [1, 1, 1, 1, 1] },
  { id: 2,  rows: [0, 0, 0, 0, 0] },
  { id: 3,  rows: [2, 2, 2, 2, 2] },
  { id: 4,  rows: [0, 1, 2, 1, 0] },
  { id: 5,  rows: [2, 1, 0, 1, 2] },
  { id: 6,  rows: [0, 0, 1, 0, 0] },
  { id: 7,  rows: [2, 2, 1, 2, 2] },
  { id: 8,  rows: [1, 2, 2, 2, 1] },
  { id: 9,  rows: [1, 0, 0, 0, 1] },
  { id: 10, rows: [1, 0, 1, 0, 1] }
];

// Text content for the Rules panel (each string can be a bullet/paragraph)
export const RULES_TEXT: string[] = [
  'Stellar Gems is played on 5 reels, 3 rows with 10 fixed paylines.',
  'Bet line wins pay in succession from leftmost reel to rightmost reel and from rightmost reel to leftmost reel, without gaps.',
  'Only the highest win per bet line is paid. Simultaneous wins on different bet lines are added.',
  'Wild symbols (WILD) appear only on reels 2, 3 and 4.',
  'Wilds expand to cover the entire reel and trigger respins. Expanded wilds remain locked while other reels respin.',
  'Additional wilds appearing during respins also expand and award extra respins, up to a maximum of 3 wild reels.',
  'Wilds substitute for all paying symbols and do not have their own pay.',
  'All payouts are shown as multiples of the bet per line.',
  'Malfunction voids all pays and plays.'
];

