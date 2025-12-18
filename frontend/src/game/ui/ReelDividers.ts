import { Container, Graphics } from 'pixi.js';
import type { ReelLayout } from '@game/config/ReelLayout';

/**
 * Vertical glowing dividers between reels, matching Starburst exactly.
 * Thin (4px) vertical lines with purple/pink neon gradient (#ff4dff → #b300ff).
 * Full height of reel frame with soft blur glow.
 */
export class ReelDividers extends Container {
  private layout!: ReelLayout;
  private dividers: Graphics[] = [];

  constructor() {
    super();
  }

  /**
   * Update dividers based on reel layout.
   */
  public updateLayout(layout: ReelLayout): void {
    this.layout = layout;
    this.rebuildDividers();
  }

  private rebuildDividers(): void {
    // Clear existing dividers
    for (const divider of this.dividers) {
      if (divider.parent) {
        divider.parent.removeChild(divider);
      }
      divider.destroy();
    }
    this.dividers = [];

    if (!this.layout) return;

    const { cols, rows, cellWidth, cellHeight, originX, originY, reelSpacing, totalHeight } = this.layout;
    const dividerWidth = 4; // Thin divider matching Starburst
    // Extend dividers past the symbols - add extra height above and below
    const extension = cellHeight * 0.3; // 30% extension on top and bottom
    const dividerHeight = totalHeight + (extension * 2); // Extended height

    // Create dividers between each reel column (4 dividers for 5 reels)
    for (let c = 0; c < cols - 1; c++) {
      // Position divider between reels, accounting for spacing
      const reelX = originX + (c + 1) * cellWidth + c * reelSpacing + reelSpacing / 2;
      
      // Divider positioned exactly between reels, extended above and below
      const divider = this.createDivider(dividerWidth, dividerHeight);
      divider.x = reelX - dividerWidth / 2;
      divider.y = originY - extension; // Start above the reel area
      this.addChild(divider);
      this.dividers.push(divider);
    }
  }

  /**
   * Create a single glowing divider with continuous gradient - bright center fading to edges.
   * Matching Starburst reference image exactly.
   */
  private createDivider(width: number, height: number): Graphics {
    const divider = new Graphics();
    const centerY = height / 2;
    const gradientSteps = 50; // More steps = smoother gradient
    const stripHeight = height / gradientSteps;

    // Create continuous gradient from center (bright) to edges (faded)
    for (let i = 0; i < gradientSteps; i++) {
      const y = i * stripHeight;
      const distanceFromCenter = Math.abs(y - centerY);
      const maxDistance = centerY;
      
      // Calculate alpha: 1.0 at center, fading to 0 at edges
      const normalizedDistance = distanceFromCenter / maxDistance;
      const alpha = Math.max(0, 1.0 - normalizedDistance);
      
      // Bright center with white highlight, fading to purple at edges
      let color = 0xff00ff; // Purple
      let finalAlpha = alpha * 0.9;
      
      // Make center brighter (white highlight)
      if (normalizedDistance < 0.3) {
        const centerBrightness = 1.0 - (normalizedDistance / 0.3);
        color = 0xffffff; // White at center
        finalAlpha = alpha * (0.5 + centerBrightness * 0.5); // Brighter at center
      } else if (normalizedDistance < 0.6) {
        // Transition from white to purple
        const transition = (normalizedDistance - 0.3) / 0.3;
        color = 0xff80ff; // Light purple
        finalAlpha = alpha * 0.8;
      }
      
      divider.beginFill(color, finalAlpha);
      divider.drawRect(0, y, width, stripHeight);
      divider.endFill();
    }

    // Add subtle outer glow on sides
    divider.beginFill(0xff4dff, 0.2);
    divider.drawRect(-2, 0, width + 4, height);
    divider.endFill();

    return divider;
  }
}
