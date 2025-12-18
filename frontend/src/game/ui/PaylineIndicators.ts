import { Container, Graphics, Text } from 'pixi.js';
import gsap from 'gsap';
import type { ReelLayout } from '@game/config/ReelLayout';
import { PAYLINES } from '@game/config/paylines';

/**
 * Payline indicators matching Starburst EXACT order and spacing.
 * Left Column: 4, 2, 6, 9, 10, 1, 8, 7, 3, 5
 * Right Column: mirrored (same order, but right side)
 * 36-40px diameter circles with yellow/gold glow.
 */
export class PaylineIndicators extends Container {
  private layout!: ReelLayout;
  private leftIndicators: Map<number, Container> = new Map();
  private rightIndicators: Map<number, Container> = new Map();
  private indicatorPositions: Map<number, { leftY: number; rightY: number; leftX: number; rightX: number }> = new Map();

  // EXACT Starburst order for left column (top to bottom)
  private readonly STARBURST_ORDER = [4, 2, 6, 9, 10, 1, 8, 7, 3, 5];

  constructor() {
    super();
  }

  /**
   * Update indicators based on reel layout.
   */
  public updateLayout(layout: ReelLayout): void {
    this.layout = layout;
    this.rebuildIndicators();
  }

  private rebuildIndicators(): void {
    // Clear existing indicators
    for (const indicator of [...this.leftIndicators.values(), ...this.rightIndicators.values()]) {
      if (indicator.parent) {
        indicator.parent.removeChild(indicator);
      }
      indicator.destroy();
    }
    this.leftIndicators.clear();
    this.rightIndicators.clear();
    this.indicatorPositions.clear();

    if (!this.layout) return;

    const { originX, originY, cellWidth, cols, totalHeight, reelSpacing } = this.layout;
    
    // Base indicator size - will vary based on position
    const baseRadius = 19;
    const leftX = originX - 80; // Left side offset matching Starburst
    const rightX = originX + cols * cellWidth + (cols - 1) * reelSpacing + 60; // Right side offset matching Starburst, accounting for spacing

    // Calculate even vertical spacing across FULL HEIGHT of reel frame
    // Add extra spacing between indicators
    const totalIndicatorHeight = baseRadius * 2 * this.STARBURST_ORDER.length;
    const extraSpacing = baseRadius * 0.4; // Additional 40% spacing between indicators
    const totalSpacing = totalHeight - totalIndicatorHeight + (extraSpacing * (this.STARBURST_ORDER.length - 1));
    const spacingBetween = totalSpacing / (this.STARBURST_ORDER.length - 1);

    // Create indicators in EXACT Starburst order
    // Size progression: smallest at edges (4, 5), biggest at center (10)
    const centerIndex = this.STARBURST_ORDER.length / 2 - 0.5; // Center is at index 4.5 (between 9 and 10)
    
    for (let i = 0; i < this.STARBURST_ORDER.length; i++) {
      const lineId = this.STARBURST_ORDER[i];
      
      // Calculate size based on distance from center
      // Numbers 4 and 5 (top and bottom) are smallest, 10 (center) is biggest
      const distanceFromCenter = Math.abs(i - centerIndex);
      const maxDistance = centerIndex;
      const sizeMultiplier = 1.0 - (distanceFromCenter / maxDistance) * 0.3; // 30% size variation
      const indicatorRadius = baseRadius * sizeMultiplier;
      
      // Calculate Y position for even distribution across full reel height, moved up slightly
      const y = originY - 30 + (i * (baseRadius * 2 + spacingBetween)) + baseRadius;

      // Left indicator
      const leftIndicator = this.createIndicator(lineId, indicatorRadius);
      leftIndicator.x = leftX;
      leftIndicator.y = y;
      this.addChild(leftIndicator);
      this.leftIndicators.set(lineId, leftIndicator);

      // Right indicator (mirror - same order)
      const rightIndicator = this.createIndicator(lineId, indicatorRadius);
      rightIndicator.x = rightX;
      rightIndicator.y = y;
      this.addChild(rightIndicator);
      this.rightIndicators.set(lineId, rightIndicator);

      // Store positions for payline drawing
      this.indicatorPositions.set(lineId, {
        leftY: y,
        rightY: y,
        leftX: leftX,
        rightX: rightX
      });
    }
  }

  /**
   * Get indicator positions for payline drawing.
   */
  public getIndicatorPositions(): Map<number, { leftY: number; rightY: number; leftX: number; rightX: number }> {
    return this.indicatorPositions;
  }

  /**
   * Create a circular glowing indicator with rounded appearance matching Starburst.
   * Size varies based on position (smaller at edges, bigger at center).
   */
  private createIndicator(lineId: number, radius: number): Container {
    const container = new Container();

    // Outer purple/pink glow ring (rounded, soft glow)
    const outerGlow = new Graphics();
    outerGlow.beginFill(0xff4dff, 0.4);
    outerGlow.drawCircle(0, 0, radius + 5);
    outerGlow.endFill();
    container.addChild(outerGlow);

    // Middle glow ring (rounded transition)
    const middleGlow = new Graphics();
    middleGlow.beginFill(0xcc66ff, 0.5);
    middleGlow.drawCircle(0, 0, radius + 2);
    middleGlow.endFill();
    container.addChild(middleGlow);

    // Main circle (purple - rounded appearance)
    const circle = new Graphics();
    circle.beginFill(0x9966ff, 0.95);
    circle.drawCircle(0, 0, radius);
    circle.endFill();
    container.addChild(circle);

    // Inner highlight (brighter center - creates rounded 3D effect)
    const highlight = new Graphics();
    highlight.beginFill(0xcc99ff, 0.7);
    highlight.drawCircle(0, 0, radius * 0.65);
    highlight.endFill();
    container.addChild(highlight);

    // Brightest center spot (rounded highlight)
    const centerSpot = new Graphics();
    centerSpot.beginFill(0xffffff, 0.6);
    centerSpot.drawCircle(0, 0, radius * 0.4);
    centerSpot.endFill();
    container.addChild(centerSpot);

    // Border stroke (rounded edge)
    const border = new Graphics();
    border.lineStyle(1.5, 0xff4dff, 0.9);
    border.drawCircle(0, 0, radius);
    container.addChild(border);

    // Number text - size scales with radius (smaller for 4 and 5, bigger for 10)
    const fontSize = Math.max(14, radius * 1.0); // Scale font with radius
    const text = new Text(lineId.toString(), {
      fill: 0xffffff,
      fontSize: fontSize,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      stroke: 0x000000,
      strokeThickness: Math.max(2, fontSize * 0.15)
    });
    text.anchor.set(0.5);
    container.addChild(text);

    // Default state: visible (matching Starburst)
    container.alpha = 0.75;
    container.scale.set(1.0);

    return container;
  }

  /**
   * Highlight winning payline indicators with pulse animation.
   */
  public highlightWinningLines(lineIds: number[]): void {
    // Reset all indicators first
    this.clearHighlights();

    // Highlight winning lines
    for (const lineId of lineIds) {
      const leftIndicator = this.leftIndicators.get(lineId);
      const rightIndicator = this.rightIndicators.get(lineId);

      if (leftIndicator) {
        leftIndicator.alpha = 1.0;
        leftIndicator.scale.set(1.25);
        gsap.to(leftIndicator.scale, {
          x: 1.35,
          y: 1.35,
          duration: 0.4,
          yoyo: true,
          repeat: 2,
          ease: 'power2.inOut'
        });
      }

      if (rightIndicator) {
        rightIndicator.alpha = 1.0;
        rightIndicator.scale.set(1.25);
        gsap.to(rightIndicator.scale, {
          x: 1.35,
          y: 1.35,
          duration: 0.4,
          yoyo: true,
          repeat: 2,
          ease: 'power2.inOut'
        });
      }
    }
  }

  /**
   * Clear all highlights, reset to default state.
   */
  public clearHighlights(): void {
    for (const indicator of [...this.leftIndicators.values(), ...this.rightIndicators.values()]) {
      indicator.alpha = 0.75;
      indicator.scale.set(1.0);
      gsap.killTweensOf(indicator.scale);
    }
  }
}
