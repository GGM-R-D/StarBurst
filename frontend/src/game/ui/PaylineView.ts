import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import gsap from 'gsap';
import type { ReelLayout } from '@game/config/ReelLayout';
import { PAYLINES } from '@game/config/paylines';
import type { Win } from '@game/types/Win';

/**
 * Draws visual paylines connecting winning symbols to the side indicators.
 * Supports Starburst-style cycling through paylines with value popups.
 */
export class PaylineView extends Container {
  private layout: ReelLayout | null = null;
  private paylineGraphics: Graphics[] = [];
  private leftIndicatorX: number = 0;
  private rightIndicatorX: number = 0;
  private indicatorPositions: Map<number, { leftY: number; rightY: number }> = new Map();
  private valuePopups: Container[] = [];
  private activePaylineGraphics: Graphics | null = null;
  private activeValuePopup: Container | null = null;
  private isAnimating: boolean = false;
  private animationCancelled: boolean = false;

  /**
   * Get a unique color for each payline based on its ID.
   * Returns an array of [outerGlowColor, middleGlowColor, mainLineColor]
   */
  private getPaylineColors(lineId: number): [number, number, number] {
    // Color palette - each payline gets a unique vibrant color
    const colorPalette: Array<[number, number, number]> = [
      [0xff4dff, 0xff66ff, 0xff00ff], // Magenta/Pink (original)
      [0x4dff4d, 0x66ff66, 0x00ff00], // Green
      [0x4d4dff, 0x6666ff, 0x0000ff], // Blue
      [0xff4d4d, 0xff6666, 0xff0000], // Red
      [0xffff4d, 0xffff66, 0xffff00], // Yellow
      [0xff4dff, 0xff66ff, 0xff00ff], // Cyan
      [0xff994d, 0xffaa66, 0xff8800], // Orange
      [0x4dffff, 0x66ffff, 0x00ffff], // Aqua
      [0xff4d99, 0xff66aa, 0xff0088], // Rose
      [0x994dff, 0xaa66ff, 0x8800ff], // Purple
    ];

    // Use modulo to cycle through colors if there are more paylines than colors
    const colorIndex = (lineId - 1) % colorPalette.length;
    return colorPalette[colorIndex];
  }

  constructor() {
    super();
  }

  public updateLayout(layout: ReelLayout, leftIndicatorX: number, rightIndicatorX: number, indicatorPositions: Map<number, { leftY: number; rightY: number }>): void {
    this.layout = layout;
    this.leftIndicatorX = leftIndicatorX;
    this.rightIndicatorX = rightIndicatorX;
    this.indicatorPositions = indicatorPositions;
  }

  public showPaylines(wins: Win[]): void {
    // DISABLED: Payline visualization removed per user request
    // Symbol highlights are working correctly, but payline lines are incorrect
    this.clearPaylines();
    return;

    if (!this.layout || wins.length === 0) return;

    const { cellWidth, cellHeight, originX, originY, reelSpacing, rowSpacing } = this.layout;

    // Group wins by line ID to draw one line per payline
    const winsByLineId = new Set<number>();
    for (const win of wins) {
      winsByLineId.add(win.lineId);
    }

    // Draw a line for each winning payline
    for (const lineId of winsByLineId) {
      const payline = PAYLINES.find(p => p.id === lineId);
      if (!payline) continue;

      // Get indicator positions for this line
      const indicatorPos = this.indicatorPositions.get(lineId);
      if (!indicatorPos) continue;

      // Create graphics for this payline
      const lineGraphics = new Graphics();
      
      // Calculate all positions along the full payline path
      const fullPath: Array<{ x: number; y: number }> = [];
      
      // Start from left indicator center
      fullPath.push({
        x: this.leftIndicatorX,
        y: indicatorPos.leftY
      });

      // Add positions for each reel along the payline (all 5 reels)
      for (let reel = 0; reel < payline.rows.length; reel++) {
        const row = payline.rows[reel];
        const x = originX + reel * (cellWidth + reelSpacing) + cellWidth / 2;
        const y = originY + row * (cellHeight + rowSpacing) + cellHeight / 2;
        fullPath.push({ x, y });
      }

      // End at right indicator center
      fullPath.push({
        x: this.rightIndicatorX,
        y: indicatorPos.rightY
      });

      // Get unique colors for this payline
      const [outerGlowColor, middleGlowColor, mainLineColor] = this.getPaylineColors(lineId);
      
      // Draw the line with glow effect (multiple layers for depth)
      // Outer glow (softer, wider)
      lineGraphics.lineStyle(8, outerGlowColor, 0.3);
      this.drawSmoothLine(lineGraphics, fullPath);
      
      // Middle glow (medium)
      lineGraphics.lineStyle(5, middleGlowColor, 0.5);
      this.drawSmoothLine(lineGraphics, fullPath);
      
      // Main line (bright, thin)
      lineGraphics.lineStyle(3, mainLineColor, 1.0);
      this.drawSmoothLine(lineGraphics, fullPath);

      this.addChild(lineGraphics);
      this.paylineGraphics.push(lineGraphics);

      // Animate the line appearance
      lineGraphics.alpha = 0;
      gsap.to(lineGraphics, {
        alpha: 1,
        duration: 0.6,
        ease: 'power2.out'
      });
    }
  }

  private drawSmoothLine(graphics: Graphics, points: Array<{ x: number; y: number }>): void {
    if (points.length < 2) return;

    graphics.moveTo(points[0].x, points[0].y);

    // Draw lines connecting all points
    for (let i = 1; i < points.length; i++) {
      graphics.lineTo(points[i].x, points[i].y);
    }
  }

  public clearPaylines(): void {
    // Cancel any ongoing animations
    this.animationCancelled = true;
    this.isAnimating = false;
    
    for (const graphics of this.paylineGraphics) {
      gsap.killTweensOf(graphics);
      graphics.destroy();
    }
    this.paylineGraphics = [];
    
    // Clear value popups
    for (const popup of this.valuePopups) {
      gsap.killTweensOf(popup);
      popup.destroy();
    }
    this.valuePopups = [];
    
    // Clear active payline and popup
    if (this.activePaylineGraphics && !this.activePaylineGraphics.destroyed) {
      gsap.killTweensOf(this.activePaylineGraphics);
      this.activePaylineGraphics.destroy();
      this.activePaylineGraphics = null;
    }
    if (this.activeValuePopup && !this.activeValuePopup.destroyed) {
      gsap.killTweensOf(this.activeValuePopup);
      this.activeValuePopup.destroy();
      this.activeValuePopup = null;
    }
    
    this.removeChildren();
  }

  /**
   * Show paylines one at a time with value popups (Starburst style).
   * Cycles through each winning payline, highlighting it and showing its value.
   * @param wins - Array of wins (one per payline)
   * @param onComplete - Callback when all paylines have been shown
   * @returns Promise that resolves when animations complete or are cancelled
   */
  /**
   * Cycle through paylines showing each win.
   * @param wins - Array of wins to display
   * @param onComplete - Callback when animation completes
   * @param isAutoSpin - If true, show for 3 seconds then complete. If false, cycle continuously until cancelled.
   */
  public async cycleThroughPaylines(wins: Win[], onComplete?: () => void, isAutoSpin: boolean = false): Promise<void> {
    // DISABLED: Payline visualization removed per user request
    // Symbol highlights are working correctly, but payline lines are incorrect
    this.clearPaylines();
    this.isAnimating = false;
    onComplete?.();
    return;

    // Reset cancellation flag
    this.animationCancelled = false;
    this.isAnimating = true;
    
    // Clear any existing paylines (but don't reset cancellation flag yet)
    for (const graphics of this.paylineGraphics) {
      gsap.killTweensOf(graphics);
      graphics.destroy();
    }
    this.paylineGraphics = [];
    for (const popup of this.valuePopups) {
      gsap.killTweensOf(popup);
      popup.destroy();
    }
    this.valuePopups = [];
    if (this.activePaylineGraphics && !this.activePaylineGraphics.destroyed) {
      gsap.killTweensOf(this.activePaylineGraphics);
      this.activePaylineGraphics.destroy();
      this.activePaylineGraphics = null;
    }
    if (this.activeValuePopup && !this.activeValuePopup.destroyed) {
      gsap.killTweensOf(this.activeValuePopup);
      this.activeValuePopup.destroy();
      this.activeValuePopup = null;
    }
    this.removeChildren();

    if (!this.layout || wins.length === 0) {
      this.isAnimating = false;
      onComplete?.();
      return;
    }

    // Group wins by line ID (in case there are multiple wins on same line)
    const winsByLineId = new Map<number, Win>();
    for (const win of wins) {
      const existing = winsByLineId.get(win.lineId);
      if (!existing || win.payout > existing.payout) {
        winsByLineId.set(win.lineId, win);
      }
    }

    const uniqueWins = Array.from(winsByLineId.values());

    // Cycle through each payline one at a time
    const isSingleLineWin = uniqueWins.length === 1;
    
    if (isAutoSpin) {
      // Auto spin mode: Show paylines once, then wait 3 seconds total, then complete
      const startTime = Date.now();
      const autoSpinDisplayDuration = 3000; // 3 seconds total
      
      for (let i = 0; i < uniqueWins.length; i++) {
        // Check if animation was cancelled
        if (this.animationCancelled) {
          console.info('[PaylineView] Animation cancelled, stopping cycle');
          this.isAnimating = false;
          return;
        }
        
        const win = uniqueWins[i];
        await this.showSinglePaylineWithValue(win.lineId, win.payout, isSingleLineWin);
        
        // Check again after showing payline
        if (this.animationCancelled) {
          console.info('[PaylineView] Animation cancelled after showing payline');
          this.isAnimating = false;
          return;
        }
        
        // Delay before showing next payline (except for the last one)
        if (i < uniqueWins.length - 1) {
          await new Promise<void>((resolve) => {
            const timeout = setTimeout(() => {
              if (!this.animationCancelled) {
                resolve();
              }
            }, 200); // 0.2 seconds between paylines
            
            // Check cancellation periodically
            const checkInterval = setInterval(() => {
              if (this.animationCancelled) {
                clearTimeout(timeout);
                clearInterval(checkInterval);
                resolve();
              }
            }, 100);
          });
        }
      }
      
      // After showing all paylines, wait for remaining time to reach 3 seconds total
      const elapsedTime = Date.now() - startTime;
      const remainingTime = Math.max(0, autoSpinDisplayDuration - elapsedTime);
      
      if (remainingTime > 0 && !this.animationCancelled) {
        await new Promise<void>((resolve) => {
          const timeout = setTimeout(() => {
            if (!this.animationCancelled) {
              resolve();
            }
          }, remainingTime);
          
          // Check cancellation periodically
          const checkInterval = setInterval(() => {
            if (this.animationCancelled) {
              clearTimeout(timeout);
              clearInterval(checkInterval);
              resolve();
            }
          }, 100);
        });
      }
      
      // Check if cancelled before finishing
      if (this.animationCancelled) {
        console.info('[PaylineView] Animation cancelled before finishing');
        this.isAnimating = false;
        return;
      }
      
      this.isAnimating = false;
      onComplete?.();
    } else {
      // Manual spin mode: Cycle continuously until cancelled (when player presses spin)
      while (!this.animationCancelled) {
        for (let i = 0; i < uniqueWins.length; i++) {
          // Check if animation was cancelled
          if (this.animationCancelled) {
            console.info('[PaylineView] Animation cancelled, stopping cycle');
            this.isAnimating = false;
            return;
          }
          
          const win = uniqueWins[i];
          await this.showSinglePaylineWithValue(win.lineId, win.payout, isSingleLineWin);
          
          // Check again after showing payline
          if (this.animationCancelled) {
            console.info('[PaylineView] Animation cancelled after showing payline');
            this.isAnimating = false;
            return;
          }
          
          // Delay before showing next payline (except for the last one before looping)
          if (i < uniqueWins.length - 1) {
            await new Promise<void>((resolve) => {
              const timeout = setTimeout(() => {
                if (!this.animationCancelled) {
                  resolve();
                }
              }, 200); // 0.2 seconds between paylines
              
              // Check cancellation periodically
              const checkInterval = setInterval(() => {
                if (this.animationCancelled) {
                  clearTimeout(timeout);
                  clearInterval(checkInterval);
                  resolve();
                }
              }, 100);
            });
          }
        }
        
        // Small delay before looping back to the first payline
        if (!this.animationCancelled) {
          await new Promise<void>((resolve) => {
            const timeout = setTimeout(() => {
              if (!this.animationCancelled) {
                resolve();
              }
            }, 500); // 0.5 second pause before looping
            
            // Check cancellation periodically
            const checkInterval = setInterval(() => {
              if (this.animationCancelled) {
                clearTimeout(timeout);
                clearInterval(checkInterval);
                resolve();
              }
            }, 100);
          });
        }
      }
      
      // Animation was cancelled (player pressed spin)
      console.info('[PaylineView] Continuous cycle ended (cancelled by player)');
      this.isAnimating = false;
      // Don't call onComplete for manual spins - they're cancelled by user action
    }
  }

  /**
   * Check if win animations are currently in progress.
   */
  public getIsAnimating(): boolean {
    return this.isAnimating;
  }

  /**
   * Show a single payline with its value popup.
   * @param lineId - The payline ID to show
   * @param amount - The win amount to display
   * @param isSingleLineWin - If true, use longer display duration (for single line wins)
   */
  private async showSinglePaylineWithValue(lineId: number, amount: number, isSingleLineWin: boolean = false): Promise<void> {
    // Check if cancelled before starting
    if (this.animationCancelled) {
      return;
    }
    
    // Clear previous active payline
    if (this.activePaylineGraphics && !this.activePaylineGraphics.destroyed) {
      gsap.killTweensOf(this.activePaylineGraphics);
      this.activePaylineGraphics.destroy();
      this.activePaylineGraphics = null;
    }
    if (this.activeValuePopup && !this.activeValuePopup.destroyed) {
      gsap.killTweensOf(this.activeValuePopup);
      this.activeValuePopup.destroy();
      this.activeValuePopup = null;
    }

    const payline = PAYLINES.find(p => p.id === lineId);
    if (!payline || !this.layout) return;

    const indicatorPos = this.indicatorPositions.get(lineId);
    if (!indicatorPos) return;

    const { cellWidth, cellHeight, originX, originY, reelSpacing, rowSpacing } = this.layout;

    // Create graphics for this payline
    const lineGraphics = new Graphics();
    
    // Calculate all positions along the full payline path
    const fullPath: Array<{ x: number; y: number }> = [];
    
    // Start from left indicator center
    fullPath.push({
      x: this.leftIndicatorX,
      y: indicatorPos.leftY
    });

    // Add positions for each reel along the payline (all 5 reels)
    for (let reel = 0; reel < payline.rows.length; reel++) {
      const row = payline.rows[reel];
      const x = originX + reel * (cellWidth + reelSpacing) + cellWidth / 2;
      const y = originY + row * (cellHeight + rowSpacing) + cellHeight / 2;
      fullPath.push({ x, y });
    }

    // End at right indicator center
    fullPath.push({
      x: this.rightIndicatorX,
      y: indicatorPos.rightY
    });

    // Get unique colors for this payline
    const [outerGlowColor, middleGlowColor, mainLineColor] = this.getPaylineColors(lineId);
    
    // Draw the line with glow effect (multiple layers for depth)
    // Outer glow (softer, wider)
    lineGraphics.lineStyle(8, outerGlowColor, 0.3);
    this.drawSmoothLine(lineGraphics, fullPath);
    
    // Middle glow (medium)
    lineGraphics.lineStyle(5, middleGlowColor, 0.5);
    this.drawSmoothLine(lineGraphics, fullPath);
    
    // Main line (bright, thin)
    lineGraphics.lineStyle(3, mainLineColor, 1.0);
    this.drawSmoothLine(lineGraphics, fullPath);

    this.addChild(lineGraphics);
    this.activePaylineGraphics = lineGraphics;

    // Animate the line appearance
    lineGraphics.alpha = 0;
    gsap.to(lineGraphics, {
      alpha: 1,
      duration: 0.1,
      ease: 'power2.out'
    });

    // Create value popup near the right indicator
    const popup = this.createValuePopup(amount, this.rightIndicatorX + 60, indicatorPos.rightY);
    this.addChild(popup);
    this.activeValuePopup = popup;

    // Animate popup appearance
    popup.alpha = 0;
    popup.scale.set(0.8);
    gsap.to(popup.scale, {
      x: 1.0,
      y: 1.0,
      duration: 0.3,
      ease: 'back.out(1.5)'
    });
    gsap.to(popup, {
      alpha: 1,
      duration: 0.3,
      ease: 'back.out(1.5)'
    });

    // Wait for display duration (check for cancellation)
    // Use longer duration for single line wins so players have more time to see it
    const displayDuration = isSingleLineWin ? 3000 : 1500; // 3 seconds for single line, 1.5 seconds for multiple
    await new Promise<void>((resolve) => {
      const timeout = setTimeout(() => {
        if (!this.animationCancelled) {
          resolve();
        }
      }, displayDuration);
      
      // Check cancellation periodically
      const checkInterval = setInterval(() => {
        if (this.animationCancelled) {
          clearTimeout(timeout);
          clearInterval(checkInterval);
          // Safely destroy if not already destroyed
          if (lineGraphics && !lineGraphics.destroyed) {
            gsap.killTweensOf(lineGraphics);
            lineGraphics.destroy();
          }
          if (popup && !popup.destroyed) {
            gsap.killTweensOf(popup);
            popup.destroy();
          }
          this.activePaylineGraphics = null;
          this.activeValuePopup = null;
          resolve();
        }
      }, 300);
    });

    // Check if cancelled before fading out
    if (this.animationCancelled) {
      if (lineGraphics && !lineGraphics.destroyed) {
        gsap.killTweensOf(lineGraphics);
        lineGraphics.destroy();
      }
      if (popup && !popup.destroyed) {
        gsap.killTweensOf(popup);
        popup.destroy();
      }
      this.activePaylineGraphics = null;
      this.activeValuePopup = null;
      return;
    }

    // Fade out
    gsap.to([lineGraphics, popup], {
      alpha: 0,
      duration: 0.5,
      ease: 'power2.in',
      onComplete: () => {
        if (!this.animationCancelled) {
          if (lineGraphics && !lineGraphics.destroyed) {
            lineGraphics.destroy();
          }
          if (popup && !popup.destroyed) {
            popup.destroy();
          }
          this.activePaylineGraphics = null;
          this.activeValuePopup = null;
        }
      }
    });
  }

  /**
   * Create a value popup showing the win amount for a payline.
   */
  private createValuePopup(amount: number, x: number, y: number): Container {
    const container = new Container();
    container.x = x;
    container.y = y;

    // Create background
    const bg = new Graphics();
    
    // Outer glow
    bg.beginFill(0xffd700, 0.3);
    bg.drawRoundedRect(-50, -20, 100, 40, 10);
    bg.endFill();
    
    // Main background
    bg.beginFill(0x5b1b7e, 0.95);
    bg.lineStyle(2, 0xffd700, 1);
    bg.drawRoundedRect(-48, -18, 96, 36, 8);
    bg.endFill();
    
    container.addChild(bg);

    // Create text
    const textStyle = new TextStyle({
      fill: 0xffd700, // Gold color
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      dropShadow: true,
      dropShadowColor: 0x000000,
      dropShadowBlur: 4,
      dropShadowDistance: 2,
      stroke: 0x000000,
      strokeThickness: 1
    });

    const text = new Text(amount.toFixed(2), textStyle);
    text.anchor.set(0.5);
    text.x = 0;
    text.y = 0;
    
    container.addChild(text);

    return container;
  }

  /**
   * Show all paylines at once (after cycling is complete).
   */
  private showAllPaylines(wins: Win[]): void {
    if (!this.layout) return;

    const { cellWidth, cellHeight, originX, originY, reelSpacing, rowSpacing } = this.layout;

    // Group wins by line ID
    const winsByLineId = new Set<number>();
    for (const win of wins) {
      winsByLineId.add(win.lineId);
    }

    // Draw all paylines (faded)
    for (const lineId of winsByLineId) {
      const payline = PAYLINES.find(p => p.id === lineId);
      if (!payline) continue;

      const indicatorPos = this.indicatorPositions.get(lineId);
      if (!indicatorPos) continue;

      const lineGraphics = new Graphics();
      
      const fullPath: Array<{ x: number; y: number }> = [];
      fullPath.push({
        x: this.leftIndicatorX,
        y: indicatorPos.leftY
      });

      for (let reel = 0; reel < payline.rows.length; reel++) {
        const row = payline.rows[reel];
        const x = originX + reel * (cellWidth + reelSpacing) + cellWidth / 2;
        const y = originY + row * (cellHeight + rowSpacing) + cellHeight / 2;
        fullPath.push({ x, y });
      }

      fullPath.push({
        x: this.rightIndicatorX,
        y: indicatorPos.rightY
      });

      const [outerGlowColor, middleGlowColor, mainLineColor] = this.getPaylineColors(lineId);
      
      lineGraphics.lineStyle(8, outerGlowColor, 0.15);
      this.drawSmoothLine(lineGraphics, fullPath);
      lineGraphics.lineStyle(5, middleGlowColor, 0.25);
      this.drawSmoothLine(lineGraphics, fullPath);
      lineGraphics.lineStyle(3, mainLineColor, 0.5);
      this.drawSmoothLine(lineGraphics, fullPath);

      this.addChild(lineGraphics);
      this.paylineGraphics.push(lineGraphics);

      lineGraphics.alpha = 0;
      gsap.to(lineGraphics, {
        alpha: 1,
        duration: 0.4,
        ease: 'power2.out'
      });
    }
  }
}

