import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import gsap from 'gsap';
import type { ReelLayout } from '@game/config/layoutConfig';

/**
 * Win display panel matching Starburst exactly.
 * Purple rounded-rectangle bar BELOW the reels (not overlapping them).
 * Width ~80% of reel frame, height 50-60px.
 */
export class WinBox extends Container {
  private panel: Graphics;
  private winText: Text;
  private currentWin: number = 0;
  private layout: ReelLayout | null = null;

  constructor() {
    super();
    this.visible = false;
    this.alpha = 0;

    // Create panel background
    this.panel = new Graphics();
    this.addChild(this.panel);

    // Create win text with proper styling matching Starburst
    const textStyle = new TextStyle({
      fill: 0xffffff,
      fontSize: 32,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      dropShadow: true,
      dropShadowColor: 0x000000,
      dropShadowBlur: 4,
      dropShadowDistance: 2,
      stroke: 0x000000,
      strokeThickness: 3
    });

    this.winText = new Text('WIN: 0.00', textStyle);
    this.winText.anchor.set(0.5);
    this.addChild(this.winText);
  }

  /**
   * Show win amount with animation matching Starburst.
   */
  public showWin(amount: number): void {
    this.currentWin = amount;
    this.updateWinText();
    this.visible = true;

    // Animate appearance (fade in with scale)
    this.scale.set(0.9);
    this.alpha = 0;
    gsap.to(this, {
      scale: 1.0,
      alpha: 1.0,
      duration: 0.3,
      ease: 'back.out(1.5)'
    });
  }

  /**
   * Hide win box with fade out.
   */
  public hideWin(): void {
    gsap.to(this, {
      alpha: 0,
      scale: 0.9,
      duration: 0.2,
      ease: 'power2.in',
      onComplete: () => {
        this.visible = false;
      }
    });
  }

  private updateWinText(): void {
    this.winText.text = `WIN: ${this.currentWin.toFixed(2)}`;
  }

  /**
   * Update layout - position panel BELOW reels, centered horizontally.
   * Width ~80% of reel frame, height 50-60px.
   */
  public updateLayout(layout?: ReelLayout, reelsY?: number, reelsHeight?: number): void {
    if (layout) {
      this.layout = layout;
    }

    if (!this.layout) return;

    const { originX, totalWidth } = this.layout;
    
    // Panel width: ~80% of reel frame width (matching Starburst)
    const panelWidth = totalWidth * 0.8;
    const panelHeight = 55; // 50-60px height matching Starburst

    // Position below reels (not overlapping)
    let panelY: number;
    if (reelsY !== undefined && reelsHeight !== undefined) {
      // Position 50px below the bottom of the reel frame
      panelY = reelsY + reelsHeight + 50;
    } else {
      // Fallback: use layout info
      panelY = this.layout.originY + this.layout.totalHeight + 50;
    }

    // Center horizontally relative to reel frame
    const panelX = originX + totalWidth / 2;

    // Redraw panel with purple gradient and bright pink outer glow matching Starburst
    this.panel.clear();

    // Bright pink outer glow (bleeding outward) - matching Starburst
    this.panel.beginFill(0xff66ff, 0.4);
    this.panel.drawRoundedRect(-panelWidth / 2 - 4, -panelHeight / 2 - 4, panelWidth + 8, panelHeight + 8, 14);
    this.panel.endFill();

    // Outer purple glow
    this.panel.beginFill(0x9b59b6, 0.35);
    this.panel.drawRoundedRect(-panelWidth / 2 - 3, -panelHeight / 2 - 3, panelWidth + 6, panelHeight + 6, 13);
    this.panel.endFill();

    // Middle glow layer
    this.panel.beginFill(0x8e44ad, 0.45);
    this.panel.drawRoundedRect(-panelWidth / 2 - 2, -panelHeight / 2 - 2, panelWidth + 4, panelHeight + 4, 12);
    this.panel.endFill();

    // Outer border (purple)
    this.panel.lineStyle(2.5, 0x9b59b6, 1);
    this.panel.beginFill(0x6a1b9a, 0.9);
    this.panel.drawRoundedRect(-panelWidth / 2, -panelHeight / 2, panelWidth, panelHeight, 12);
    this.panel.endFill();

    // Inner gradient effect (lighter purple)
    this.panel.beginFill(0x8e44ad, 0.75);
    this.panel.drawRoundedRect(-panelWidth / 2 + 2, -panelHeight / 2 + 2, panelWidth - 4, panelHeight - 4, 10);
    this.panel.endFill();

    // Brightest center highlight
    this.panel.beginFill(0x9b59b6, 0.6);
    this.panel.drawRoundedRect(-panelWidth / 2 + 4, -panelHeight / 2 + 4, panelWidth - 8, panelHeight - 8, 8);
    this.panel.endFill();

    // Position panel (centered on container)
    this.panel.x = 0;
    this.panel.y = 0;

    // Position text (centered in panel)
    this.winText.x = 0;
    this.winText.y = 0;

    // Position container relative to reel frame
    this.x = panelX;
    this.y = panelY;
  }
}
