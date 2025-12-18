/**
 * Bet amount utilities for handling bet increments and precision.
 */

/**
 * Calculate the next bet amount based on custom increment rules:
 * - R0 - R2: increments of 0.10
 * - R2 - R5: increments of 0.50
 * - R5 - R20: increments of R1
 * - R20 - R50: increments of R5
 * - R50 - R100: increments of R10
 */
export function getNextBetAmount(currentBet: number, maxBet: number = 100): number {
  const rounded = roundToTwoDecimals(currentBet);
  
  if (rounded < 2.0) {
    // R0 - R2: increments of 0.10
    const next = rounded + 0.10;
    return Math.min(next, 2.0);
  } else if (rounded < 5.0) {
    // R2 - R5: increments of 0.50
    const next = rounded + 0.50;
    return Math.min(next, 5.0);
  } else if (rounded < 20.0) {
    // R5 - R20: increments of R1
    const next = Math.ceil(rounded) + 1.0;
    return Math.min(next, 20.0);
  } else if (rounded < 50.0) {
    // R20 - R50: increments of R5
    const next = Math.ceil(rounded / 5.0) * 5.0;
    if (next <= rounded) {
      return rounded + 5.0;
    }
    return Math.min(next, 50.0);
  } else if (rounded < maxBet) {
    // R50 - R100: increments of R10
    const next = Math.ceil(rounded / 10.0) * 10.0;
    if (next <= rounded) {
      return rounded + 10.0;
    }
    return Math.min(next, maxBet);
  }
  
  return maxBet;
}

/**
 * Calculate the previous bet amount based on custom increment rules.
 */
export function getPreviousBetAmount(currentBet: number, minBet: number = 0.10): number {
  const rounded = roundToTwoDecimals(currentBet);
  
  if (rounded <= 2.0) {
    // R0 - R2: decrements of 0.10
    const prev = rounded - 0.10;
    return Math.max(prev, minBet);
  } else if (rounded <= 5.0) {
    // R2 - R5: decrements of 0.50
    const prev = rounded - 0.50;
    return Math.max(prev, 2.0);
  } else if (rounded <= 20.0) {
    // R5 - R20: decrements of R1
    const prev = Math.floor(rounded) - 1.0;
    return Math.max(prev, 5.0);
  } else if (rounded <= 50.0) {
    // R20 - R50: decrements of R5
    const prev = Math.floor(rounded / 5.0) * 5.0;
    if (prev >= rounded) {
      return rounded - 5.0;
    }
    return Math.max(prev, 20.0);
  } else {
    // R50 - R100: decrements of R10
    const prev = Math.floor(rounded / 10.0) * 10.0;
    if (prev >= rounded) {
      return rounded - 10.0;
    }
    return Math.max(prev, 50.0);
  }
}

/**
 * Round a number to exactly 2 decimal places to avoid floating-point precision issues.
 * This ensures compatibility with backend decimal(20,2) format.
 */
export function roundToTwoDecimals(value: number): number {
  return Math.round(value * 100) / 100;
}

/**
 * Generate all valid bet amounts based on increment rules.
 */
export function generateBetAmounts(minBet: number = 0.10, maxBet: number = 100): number[] {
  const amounts: number[] = [];
  let current = minBet;
  
  while (current <= maxBet) {
    amounts.push(roundToTwoDecimals(current));
    
    if (current < 2.0) {
      current += 0.10;
    } else if (current < 5.0) {
      current += 0.50;
    } else if (current < 20.0) {
      current += 1.0;
    } else if (current < 50.0) {
      current += 5.0;
    } else {
      current += 10.0;
    }
  }
  
  return amounts;
}

