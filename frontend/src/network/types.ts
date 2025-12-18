// RGS-compliant types based on Client-RGS.txt specification

export interface StartRequest {
  languageId: string;
  client: string;
  funMode: number;
  token: string;
}

export interface Player {
  sessionId: string;
  id: string;
  balance: number;
}

export interface Client {
  type: string;
  ip: string;
  country: {
    code: string;
    name: string;
  };
}

export interface Currency {
  symbol: string;
  isoCode: string;
  name: string;
  separator: {
    decimal: string;
    thousand: string;
  };
  decimals: number;
}

export interface GameBet {
  default: number;
  levels: number[];
}

export interface GameSettings {
  isAutoplay: string;
  isSlamStop: string;
  isBuyFeatures: string;
  isTurboSpin: string;
  isRealityCheck: string;
  minSpin: string;
  maxSpin: string;
}

export interface GameConfig {
  startScreen: string | null;
  settings: GameSettings;
}

export interface FreeSpins {
  amount: number;
  left: number;
  betValue: number;
  roundWin: number;
  totalWin: number;
  totalBet: number;
}

export interface PromoFreeSpins {
  amount: number;
  left: number;
  betValue: number;
  isPromotion: boolean;
  totalWin: number;
  totalBet: number;
}

export interface Feature {
  name: string;
  type: string;
}

export interface BetLevel {
  index: number;
  value: number;
}

export interface LastPlay {
  betLevel: BetLevel;
  results: unknown[];
}

export interface Game {
  rtp: number;
  mode: number;
  bet: GameBet;
  funMode: boolean;
  maxWinCap: number;
  config: GameConfig;
  freeSpins: FreeSpins;
  promoFreeSpins: PromoFreeSpins;
  feature: Feature;
  lastPlay: LastPlay;
  results?: unknown; // Game engine results
}

export interface StartResponse {
  statusCode: number;
  message: string;
  player: Player;
  client: Client;
  currency: Currency;
  game: Game;
}

export interface BetAmount {
  betType: string; // e.g., "BASE"
  amount: number;
}

export interface PlayRequest {
  sessionId: string;
  baseBet: number;
  betMode?: string; // Optional, defaults to "standard"
  bets: BetAmount[];
  userPayload?: unknown;
  lastResponse?: unknown;
}

export interface Transaction {
  withdraw: string;
  deposit: string;
}

export interface MaxWinCap {
  achieved: boolean;
  value: number;
  realWin: string;
}

export interface PlayResponsePlayer {
  sessionId: string;
  roundId: string;
  transaction: Transaction;
  prevBalance: number;
  balance: number;
  bet: number;
  win: number;
  currencyId: string;
}

export interface PlayResponseGame {
  results: unknown; // Game engine results
  mode: number;
  maxWinCap: MaxWinCap;
}

export interface PlayResponseFreeSpins {
  amount: number;
  left: number;
  isPromotion: boolean;
  roundWin: number;
  totalWin: number;
  totalBet: number;
  won: number;
}

export interface PlayResponsePromoFreeSpins {
  amount: number;
  left: number;
  betValue: number;
  level: number;
  totalWin: number;
  totalBet: number;
}

export interface PlayResponseFeature {
  name: string;
  type: string;
  isClosure: number;
}

export interface PlayResponse {
  statusCode: number;
  message: string;
  player: PlayResponsePlayer;
  game: PlayResponseGame;
  freeSpins: PlayResponseFreeSpins;
  promoFreeSpins: PlayResponsePromoFreeSpins;
  jackpots: unknown[];
  feature: PlayResponseFeature;
}

export interface BalanceRequest {
  playerId: string;
}

export interface BalanceResponse {
  statusCode: number;
  message: string;
  balance: number;
}

// Legacy types for backward compatibility (can be removed after migration)
export interface WildPosition {
  reelIndex: number;
  rowIndex: number;
}

