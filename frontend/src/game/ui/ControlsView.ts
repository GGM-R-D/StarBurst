import { Container, Graphics, Text } from 'pixi.js';

export interface ControlsViewEvents {
  onSpin?: () => void;
  onSpeedChange?: (speed: 1 | 2 | 3) => void;
  onBetDecrease?: () => void;
  onBetIncrease?: () => void;
}

/**
 * Control bar matching Starburst exactly.
 * This is an alternative to BottomBar - kept for compatibility.
 * For new code, use BottomBar instead.
 */
export class ControlsView extends Container {
  private spinButton: Graphics;
  private spinLabel: Text;
  private callbacks: ControlsViewEvents;

  constructor(callbacks: ControlsViewEvents = {}) {
    super();
    this.callbacks = callbacks;
    
    // Create spin button matching Starburst (large green glowing circle)
    this.spinButton = this.createSpinButton();
    this.spinButton.eventMode = 'static';
    this.spinButton.cursor = 'pointer';

    this.spinLabel = new Text('SPIN', {
      fill: 0xffffff,
      fontSize: 32,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      stroke: 0x000000,
      strokeThickness: 3
    });
    this.spinLabel.anchor.set(0.5);
    this.spinLabel.position.set(0, 0);
    this.spinButton.addChild(this.spinLabel);

    this.spinButton.on('pointertap', () => this.callbacks.onSpin?.());

    this.addChild(this.spinButton);
  }

  /**
   * Create large green glowing spin button matching Starburst exactly.
   */
  private createSpinButton(): Graphics {
    const button = new Graphics();
    const radius = 75; // Large size matching Starburst

    // Drop shadow (below button)
    const shadow = new Graphics();
    shadow.beginFill(0x000000, 0.4);
    shadow.drawEllipse(0, radius + 8, radius * 1.2, radius * 0.3);
    shadow.endFill();
    button.addChild(shadow);

    // Outer neon green glow ring (bleeding outward)
    button.beginFill(0x00ff00, 0.4);
    button.drawCircle(0, 0, radius + 12);
    button.endFill();

    // Middle glow ring
    button.beginFill(0x00cc00, 0.5);
    button.drawCircle(0, 0, radius + 6);
    button.endFill();

    // Main circle (bright green gradient effect)
    button.beginFill(0x00ff00, 1);
    button.drawCircle(0, 0, radius);
    button.endFill();

    // Inner highlight (brighter center)
    button.beginFill(0x66ff66, 0.8);
    button.drawCircle(0, 0, radius * 0.75);
    button.endFill();

    // Brightest center
    button.beginFill(0x99ff99, 0.6);
    button.drawCircle(0, 0, radius * 0.5);
    button.endFill();

    // Border stroke
    button.lineStyle(2, 0x00cc00, 1);
    button.drawCircle(0, 0, radius);
    button.endFill();

    return button;
  }
}
