import { Container, Text, Graphics, Sprite, Texture } from 'pixi.js';
import { getAssetUrl } from '@game/config/assetsManifest';

export class HUDView extends Container {
  private balanceText: Text;
  private betText: Text;
  private spinButton: Graphics | Sprite;
  private speedButton: Graphics;
  private speedLabel: Text;
  private currentSpeed: 1 | 2 | 3 = 1;
  private screenWidth: number = 1920;
  private screenHeight: number = 1080;
  private onSpin?: () => void;
  private onSpeedChange?: (speed: 1 | 2 | 3) => void;

  constructor() {
    super();

    // Balance label
    this.balanceText = new Text('BAL: 1 000.00', {
      fill: 0xffffff,
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });

    // Bet label
    this.betText = new Text('BET: 1.00', {
      fill: 0xffffff,
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });

    // Spin button
    this.spinButton = this.createSpinButton();

    this.addChild(this.balanceText);
    this.addChild(this.betText);
    this.addChild(this.spinButton);

    // Speed button - styled to match Starburst (dark background, white text)
    this.speedButton = new Graphics();
    this.speedButton.beginFill(0x1a1a1a, 0.95);
    this.speedButton.lineStyle(2, 0x666666, 0.8);
    this.speedButton.drawRoundedRect(-60, -20, 120, 40, 10);
    this.speedButton.endFill();
    this.speedButton.eventMode = 'static';
    this.speedButton.cursor = 'pointer';

    this.speedLabel = new Text('SPEED: x1', {
      fill: 0xffffff,
      fontSize: 14,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    this.speedLabel.anchor.set(0.5);
    this.speedButton.addChild(this.speedLabel);

    this.addChild(this.speedButton);

    // Enable interaction on the spin button
    this.spinButton.eventMode = 'static';
    this.spinButton.buttonMode = true;
    this.spinButton.on('pointertap', () => {
      if (this.onSpin) {
        this.onSpin();
      }
    });

    this.speedButton.on('pointertap', () => {
      this.cycleSpeed();
    });

    this.updateLayout();
  }

  private createSpinButton(): Graphics | Sprite {
    // Try to load button asset, fallback to graphics
    try {
      const texture = Texture.from(getAssetUrl('/Control_Panel/spin_btn.png'));
      if (texture.valid) {
        const sprite = new Sprite(texture);
        sprite.anchor.set(0.5);
        return sprite;
      }
    } catch (error) {
      // Fallback to graphics
    }

    // Create a simple button using graphics - green glow matching Starburst
    const button = new Graphics();
    // Outer green glow
    button.beginFill(0x00ff00, 0.3);
    button.drawRoundedRect(-80, -25, 160, 50, 8);
    button.endFill();
    // Main button (green gradient)
    button.beginFill(0x00cc00, 1);
    button.lineStyle(2, 0x00cc00, 1);
    button.drawRoundedRect(-80, -25, 160, 50, 8);
    button.endFill();

    const buttonText = new Text('SPIN', {
      fill: 0x000000,
      fontSize: 28,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    buttonText.anchor.set(0.5);
    button.addChild(buttonText);

    // Graphics doesn't have anchor, so we'll position it manually in updateLayout
    return button;
  }

  updateLayout(width?: number, height?: number): void {
    if (width) this.screenWidth = width;
    if (height) this.screenHeight = height;

    const hudY = this.screenHeight - 140;
    const padding = 40;

    // Position balance text (left side)
    this.balanceText.x = padding;
    this.balanceText.y = hudY;

    // Position bet text (left side, below balance)
    this.betText.x = padding;
    this.betText.y = hudY + 35;

    // Position spin button (center bottom)
    if (this.spinButton instanceof Sprite) {
      this.spinButton.x = this.screenWidth / 2;
      this.spinButton.y = hudY + 25;
    } else if (this.spinButton instanceof Graphics) {
      // Graphics button is drawn with -80 offset, so center it properly
      this.spinButton.x = this.screenWidth / 2;
      this.spinButton.y = hudY + 25;
    }

    // Position speed button to the right of spin
    this.speedButton.x = this.screenWidth / 2 + 150;
    this.speedButton.y = hudY + 25;
  }

  public setSpinHandler(handler: () => void): void {
    this.onSpin = handler;
  }

  public setSpinEnabled(enabled: boolean): void {
    this.spinButton.eventMode = enabled ? 'static' : 'none';
    this.spinButton.cursor = enabled ? 'pointer' : 'default';
    this.spinButton.alpha = enabled ? 1 : 0.5;
  }

  private cycleSpeed(): void {
    if (this.currentSpeed === 1) this.currentSpeed = 2;
    else if (this.currentSpeed === 2) this.currentSpeed = 3;
    else this.currentSpeed = 1;

    this.updateSpeedLabel();

    if (this.onSpeedChange) {
      this.onSpeedChange(this.currentSpeed);
    }
  }

  private updateSpeedLabel(): void {
    this.speedLabel.text = `SPEED: x${this.currentSpeed}`;
  }

  public setSpeedChangeHandler(handler: (speed: 1 | 2 | 3) => void): void {
    this.onSpeedChange = handler;
  }

  public getCurrentSpeed(): 1 | 2 | 3 {
    return this.currentSpeed;
  }

  setBalance(value: number, currency: string = 'EUR'): void {
    this.balanceText.text = `BAL: ${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  setBet(value: number, currency: string = 'EUR'): void {
    this.betText.text = `BET: ${value.toFixed(2)}`;
  }
}

