import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import gsap from 'gsap';
import type { ReelLayout } from '@game/config/ReelLayout';

/**
 * Win panel matching Starburst exactly.
 * Purple rounded rectangle panel BELOW the reels (not overlapping).
 * Width: 80% of reel width, Height: 50-60px.
 * Positioned ~32px below bottom frame edge.
 */
export class WinPanel extends Container {
  private panel: Graphics;
  private winText: Text;
  private currentWin: number = 0;
  private layout: ReelLayout | null = null;

  constructor() {
    super();
    this.visible = true; // Always visible, showing "WIN 0.00" by default
    this.alpha = 1.0;

    // Create panel background
    this.panel = new Graphics();
    this.addChild(this.panel);

    // Create win text matching EXACT Starburst specification
    const textStyle = new TextStyle({
      fill: '#fff',
      fontSize: 28,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      dropShadow: true,
      dropShadowColor: 0x000000,
      dropShadowBlur: 4,
      dropShadowDistance: 2
    });

    this.winText = new Text('WIN 0.00', textStyle);
    this.winText.anchor.set(0.5);
    this.addChild(this.winText);
  }

  /**
   * Show win amount - always visible, just update text.
   */
  public showWin(amount: number): void {
    this.currentWin = amount;
    this.updateWinText();
    this.visible = true;
    this.alpha = 1.0;
  }

  /**
   * Hide win panel - set to 0.00 but keep visible.
   */
  public hideWin(): void {
    this.currentWin = 0;
    this.updateWinText();
    this.visible = true;
    this.alpha = 1.0;
  }

  private updateWinText(): void {
    this.winText.text = `WIN ${this.currentWin.toFixed(2)}`;
  }

  /**
   * Update layout - position panel BELOW reels matching EXACT Starburst specification.
   * Width: 60% of screen width, Height: 50px.
   * Positioned 20px below bottom frame edge.
   */
  public updateLayout(layout?: ReelLayout, reelsY?: number, reelsHeight?: number, screenWidth?: number): void {
    if (layout) {
      this.layout = layout;
    }

    if (!this.layout) return;

    const { originY, totalHeight } = this.layout;
    
    // Panel width: 45% of screen width (matching Starburst proportions)
    const panelWidth = (screenWidth ?? 1920) * 0.45;
    const panelHeight = 50; // Matching EXACT specification: 50px

    // Position below reels (moved down)
    let panelY: number;
    if (reelsY !== undefined && reelsHeight !== undefined) {
      panelY = reelsY + reelsHeight + 180; // Moved down from 140 to 180
    } else {
      panelY = originY + totalHeight + 180;
    }

    // Center horizontally - container x should be screen center minus half panel width
    const panelX = ((screenWidth ?? 1920) / 2) - (panelWidth / 2);

    // Redraw panel with purple gradient and pink glow matching image exactly
    this.panel.clear();
    
    // Outer pink/purple glow
    this.panel.beginFill(0xff66ff, 0.4);
    this.panel.drawRoundedRect(-4, -4, panelWidth + 8, panelHeight + 8, 24);
    this.panel.endFill();
    
    // Middle glow layer
    this.panel.beginFill(0xff33ff, 0.3);
    this.panel.drawRoundedRect(-2, -2, panelWidth + 4, panelHeight + 4, 22);
    this.panel.endFill();
    
    // Main panel (purple gradient)
    this.panel.beginFill(0x5b1b7e, 0.95);
    this.panel.drawRoundedRect(0, 0, panelWidth, panelHeight, 20);
    this.panel.endFill();
    
    // Inner highlight
    this.panel.beginFill(0x6b1d7c, 0.6);
    this.panel.drawRoundedRect(2, 2, panelWidth - 4, panelHeight - 4, 18);
    this.panel.endFill();

    // Position panel (centered on container origin)
    this.panel.x = 0;
    this.panel.y = 0;

    // Position text (centered in panel)
    this.winText.anchor.set(0.5);
    this.winText.x = panelWidth / 2;
    this.winText.y = panelHeight / 2;

    // Position container (centered horizontally, below reels)
    this.x = panelX;
    this.y = panelY;
  }
}

