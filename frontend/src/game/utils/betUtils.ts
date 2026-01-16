/**
 * Bet amount utilities for handling bet increments and precision.
 * Uses exact bet levels from backend configuration.
 */

// Bet levels from backend config: [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4, 1.5, 1.6, 1.8, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100]
const BET_LEVELS = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4, 1.5, 1.6, 1.8, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100];

/**
 * Get the closest bet level to the current bet.
 */
function getClosestBetLevel(bet: number): number {
  const rounded = roundToTwoDecimals(bet);
  let closest = BET_LEVELS[0];
  let minDiff = Math.abs(rounded - closest);
  
  for (const level of BET_LEVELS) {
    const diff = Math.abs(rounded - level);
    if (diff < minDiff) {
      minDiff = diff;
      closest = level;
    }
  }
  
  return closest;
}

/**
 * Calculate the next bet amount from the bet levels array.
 */
export function getNextBetAmount(currentBet: number, maxBet: number = 100.0): number {
  const rounded = roundToTwoDecimals(currentBet);
  const currentIndex = BET_LEVELS.findIndex(level => level >= rounded);
  
  if (currentIndex === -1) {
    // Current bet is less than all levels, return first level
    return BET_LEVELS[0];
  }
  
  if (currentIndex < BET_LEVELS.length - 1) {
    // Return next level
    return Math.min(BET_LEVELS[currentIndex + 1], maxBet);
  }
  
  // Already at max, return current
  return Math.min(BET_LEVELS[BET_LEVELS.length - 1], maxBet);
}

/**
 * Calculate the previous bet amount from the bet levels array.
 */
export function getPreviousBetAmount(currentBet: number, minBet: number = 0.1): number {
  const rounded = roundToTwoDecimals(currentBet);
  const currentIndex = BET_LEVELS.findIndex(level => level >= rounded);
  
  if (currentIndex === 0 || currentIndex === -1) {
    // At first level or below all levels, return min
    return Math.max(BET_LEVELS[0], minBet);
  }
  
  // Return previous level
  return Math.max(BET_LEVELS[currentIndex - 1], minBet);
}

/**
 * Round a number to exactly 2 decimal places to avoid floating-point precision issues.
 * This ensures compatibility with backend decimal(20,2) format.
 */
export function roundToTwoDecimals(value: number): number {
  return Math.round(value * 100) / 100;
}

/**
 * Generate all valid bet amounts from the bet levels array.
 */
export function generateBetAmounts(minBet: number = 0.1, maxBet: number = 100.0): number[] {
  return BET_LEVELS.filter(level => level >= minBet && level <= maxBet);
}

/**
 * Get all bet levels (exported for use in other components).
 */
export function getBetLevels(): number[] {
  return [...BET_LEVELS];
}

