import { Container, Text, TextStyle, Graphics } from 'pixi.js';
import gsap from 'gsap';

/**
 * Win counter that displays individual win amounts as they're counted.
 * Shows as floating text above the reels.
 */
export class WinCounter extends Container {
  private counterText: Text | null = null;
  private background: Graphics | null = null;
  private screenWidth: number = 1920;
  private screenHeight: number = 1080;
  private activeCounterTween: gsap.core.Tween | null = null;
  private activeFadeOutTween: gsap.core.Tween | null = null;
  private currentLineId: number = 0;
  private currentAmount: number = 0;
  private onCompleteCallback: (() => void) | undefined = undefined;

  constructor() {
    super();
    this.visible = true;
    this.alpha = 1.0;
  }

  /**
   * Show a win counter for a specific line win.
   * @param lineId - The payline ID
   * @param amount - The win amount for this line
   * @param onComplete - Callback when animation completes
   */
  public showLineWin(lineId: number, amount: number, onComplete?: () => void): void {
    // Remove any existing counter
    this.clearCounter();

    // Store values for callback
    this.currentLineId = lineId;
    this.currentAmount = amount;
    this.onCompleteCallback = onComplete;

    // Ensure container is visible and positioned
    this.visible = true;
    this.alpha = 1.0;
    this.x = this.screenWidth / 2;
    this.y = this.screenHeight * 0.5; // Center of screen

    // Create background panel with glow effect
    this.background = new Graphics();
    
    // Outer glow (pink/purple)
    this.background.beginFill(0xff00ff, 0.3);
    this.background.drawRoundedRect(-200, -50, 400, 100, 25);
    this.background.endFill();
    
    // Middle glow
    this.background.beginFill(0xff66ff, 0.4);
    this.background.drawRoundedRect(-195, -45, 390, 90, 22);
    this.background.endFill();
    
    // Main background (purple/dark)
    this.background.beginFill(0x5b1b7e, 0.95);
    this.background.lineStyle(3, 0xff00ff, 1);
    this.background.drawRoundedRect(-190, -40, 380, 80, 20);
    this.background.endFill();
    
    this.addChild(this.background);

    // Create counter text with nicer styling
    const textStyle = new TextStyle({
      fill: 0xffd700, // Gold color
      fontSize: 52,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      dropShadow: true,
      dropShadowColor: 0x000000,
      dropShadowBlur: 8,
      dropShadowDistance: 3,
      stroke: 0x000000,
      strokeThickness: 2
    });

    this.counterText = new Text(`LINE ${lineId}: ${amount.toFixed(2)}`, textStyle);
    this.counterText.anchor.set(0.5);
    
    // Position text relative to container
    this.counterText.x = 0;
    this.counterText.y = 0;
    
    this.addChild(this.counterText);

    // Start invisible for fade-in animation
    this.counterText.alpha = 0;
    this.counterText.scale.set(0.5);
    if (this.background) {
      this.background.alpha = 0;
      this.background.scale.set(0.5);
    }

    // Count up animation - slower and smoother
    let currentValue = 0;
    const targetValue = amount;
    const duration = 2.0; // 2 seconds to count up (slower)
    
    this.activeCounterTween = gsap.to({}, {
      duration: duration,
      ease: 'power1.out', // Smoother easing
      onUpdate: () => {
        if (this.counterText && this.activeCounterTween) {
          const progress = this.activeCounterTween.progress();
          currentValue = targetValue * progress;
          this.counterText.text = `LINE ${lineId}: ${currentValue.toFixed(2)}`;
        }
      },
      onComplete: () => {
        // Ensure final value is exact
        if (this.counterText) {
          this.counterText.text = `LINE ${lineId}: ${targetValue.toFixed(2)}`;
        }
        // Start fade out sequence after counting completes
        this.startFadeOutSequence();
      }
    });

    // Fade in and scale up animation
    const targets = this.background ? [this.counterText, this.background] : [this.counterText];
    
    gsap.to(targets, {
      alpha: 1,
      duration: 0.5,
      ease: 'power2.out'
    });
    
    // Animate text scale
    if (!this.counterText) return;
    
    gsap.to(this.counterText.scale, {
      x: 1.15,
      y: 1.15,
      duration: 0.5,
      ease: 'back.out(1.7)',
      onComplete: () => {
        if (!this.counterText) return;
        // Scale back to normal
        gsap.to(this.counterText.scale, {
          x: 1.0,
          y: 1.0,
          duration: 0.3,
          ease: 'power2.inOut'
        });
      }
    });
    
    // Also animate background scale
    if (this.background) {
      gsap.to(this.background.scale, {
        x: 1.15,
        y: 1.15,
        duration: 0.5,
        ease: 'back.out(1.7)'
      });
    }
  }

  /**
   * Start the fade out sequence after counting completes
   */
  private startFadeOutSequence(): void {
    const targets = this.background ? [this.counterText, this.background] : [this.counterText];
    if (!targets.length || !this.counterText) return;

    // Hold for a moment, then fade out
    this.activeFadeOutTween = gsap.to(targets, {
      alpha: 0,
      duration: 0.5,
      delay: 1.5, // Hold for 1.5 seconds after counting completes
      ease: 'power2.in',
      onComplete: () => {
        // Scale down while fading
        if (this.counterText) {
          gsap.to(this.counterText.scale, {
            x: 0.9,
            y: 0.9,
            duration: 0.5,
            ease: 'power2.in'
          });
        }
        if (this.background) {
          gsap.to(this.background.scale, {
            x: 0.9,
            y: 0.9,
            duration: 0.5,
            ease: 'power2.in',
            onComplete: () => {
              this.clearCounter();
              this.onCompleteCallback?.();
            }
          });
        } else {
          this.clearCounter();
          this.onCompleteCallback?.();
        }
      }
    });
  }

  private clearCounter(): void {
    // Kill all active tweens
    if (this.activeCounterTween) {
      this.activeCounterTween.kill();
      this.activeCounterTween = null;
    }
    if (this.activeFadeOutTween) {
      this.activeFadeOutTween.kill();
      this.activeFadeOutTween = null;
    }

    if (this.counterText) {
      gsap.killTweensOf(this.counterText);
      gsap.killTweensOf(this.counterText.scale);
      this.removeChild(this.counterText);
      this.counterText.destroy();
      this.counterText = null;
    }
    if (this.background) {
      gsap.killTweensOf(this.background);
      gsap.killTweensOf(this.background.scale);
      this.removeChild(this.background);
      this.background.destroy();
      this.background = null;
    }

    // Reset callback
    this.onCompleteCallback = undefined;
  }

  public updateLayout(width: number, height: number): void {
    this.screenWidth = width;
    this.screenHeight = height;
    
    // Update container position if counter is showing
    if (this.counterText) {
      this.x = width / 2;
      this.y = height * 0.5; // Center of screen
    }
  }
}

