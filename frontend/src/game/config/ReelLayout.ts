export const REEL_COLS = 5;
export const REEL_ROWS = 3;

export interface ReelLayout {
  cols: number;
  rows: number;
  cellWidth: number;
  cellHeight: number;
  originX: number;
  originY: number;
  totalWidth: number;
  totalHeight: number;
  reelSpacing: number; // Spacing between reels
  rowSpacing: number; // Spacing between rows
}

/**
 * Simple 5x3 layout:
 * - Reels use ~40% of screen height.
 * - Grid is centered in the screen.
 * - Cells are square so symbols don't distort.
 */
export function computeReelLayout(screenWidth: number, screenHeight: number): ReelLayout {
  const cols = REEL_COLS;
  const rows = REEL_ROWS;

  // Use a fraction of the screen height so the grid fits inside the frame.
  // Tweak 0.40 if the grid is still slightly too big/small.
  const reelsHeight = Math.floor(screenHeight * 0.39);
  const cellHeight = Math.floor(reelsHeight / rows);

  // Make cells square; width derived from height.
  const cellWidth = cellHeight;

  // Add spacing between reels (spread them out to match Starburst)
  const reelSpacing = cellWidth * 0.6; // 60% spacing between reels - matching Starburst image exactly
  const totalReelSpacing = reelSpacing * (cols - 1);
  
  // Add spacing between rows (vertical spacing) and padding above/below symbols
  const rowSpacing = cellHeight * 0.4; // 40% spacing between rows (includes above/below padding)
  const totalRowSpacing = rowSpacing * (rows - 1);
  
  const reelWidth = cols * cellWidth + totalReelSpacing;
  const reelHeight = rows * cellHeight + totalRowSpacing;

  // Center the whole grid horizontally.
  const originX = Math.floor((screenWidth - reelWidth) / 2);

  // Center vertically, then move up slightly
  const centerY = Math.floor((screenHeight - reelHeight) / 2);
  const originY = centerY - 40; // Move reels up by 40px (reduced from 80px to move them down)

  return {
    cols,
    rows,
    cellWidth,
    cellHeight,
    originX,
    originY,
    totalWidth: reelWidth,
    totalHeight: reelHeight,
    reelSpacing: reelSpacing,
    rowSpacing: rowSpacing
  };
}

