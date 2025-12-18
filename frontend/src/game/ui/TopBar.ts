import { Container, Graphics, Text, Sprite, Texture } from 'pixi.js';
import type { ReelLayout } from '@game/config/ReelLayout';
import { getAssetUrl } from '@game/config/assetsManifest';

export interface TopBarCallbacks {
  onInfo?: () => void;
  onHelp?: () => void;
  onSettings?: () => void;
  onSoundToggle?: () => void;
  onFeature?: () => void;
}

/**
 * Top bar matching Starburst exactly - full width with centered logo and right-aligned menu buttons.
 */
export class TopBar extends Container {
  private logo: Sprite | null = null;
  private infoButton: Container;
  private helpButton: Container;
  private settingsButton: Container;
  private soundButton: Container;
  private featureButton: Container;
  private soundEnabled = true;
  private callbacks: TopBarCallbacks;
  private screenWidth: number = 1920;
  private screenHeight: number = 1080;
  private reelLayout: ReelLayout | null = null;

  constructor(callbacks: TopBarCallbacks = {}) {
    super();
    this.callbacks = callbacks;

    // Create logo - load from image
    this.createLogo();

    // Create menu buttons - perfect circles matching EXACT Starburst specification
    this.infoButton = this.createMenuButton('i');
    this.helpButton = this.createMenuButton('?');
    this.settingsButton = this.createMenuButton('⚙');
    this.soundButton = this.createMenuButton('🔊');
    this.featureButton = this.createMenuButton('⭐');

    // Add logo if it loaded
    if (this.logo) {
      this.addChild(this.logo);
    }
    this.addChild(this.infoButton);
    this.addChild(this.helpButton);
    this.addChild(this.settingsButton);
    this.addChild(this.soundButton);
    this.addChild(this.featureButton);

    // Setup interactions
    this.setupInteractions();

    this.updateLayout();
  }

  private createLogo(): void {
    // Load logo asset
    try {
      const logoPath = getAssetUrl('/images/Logo.webp');
      const texture = Texture.from(logoPath);
      
      // Check if texture is already loaded
      if (texture.baseTexture.valid) {
        this.logo = new Sprite(texture);
        this.logo.anchor.set(0.3, 0.3);
        // Scale down the logo to make it smaller
        this.logo.scale.set(0.1);
        if (!this.logo.parent) {
          this.addChild(this.logo);
        }
      } else {
        // Wait for texture to load
        const onLoaded = () => {
          if (!this.logo) {
            this.logo = new Sprite(texture);
            this.logo.anchor.set(0.5, 0.6);
            // Scale down the logo to make it smaller
            this.logo.scale.set(0.4);
            if (!this.logo.parent) {
              this.addChild(this.logo);
            }
            this.updateLayout();
          }
          texture.baseTexture.off('loaded', onLoaded);
        };
        texture.baseTexture.on('loaded', onLoaded);
        
        // Also handle error case
        texture.baseTexture.on('error', () => {
          console.warn('Failed to load logo image');
          texture.baseTexture.off('error', () => {});
        });
      }
    } catch (error) {
      console.warn('Error loading logo:', error);
    }
  }

  /**
   * Create perfect circular button matching EXACT Starburst specification.
   */
  private createMenuButton(iconText: string): Container {
    const c = new Graphics();
    c.beginFill(0x1a1a1a);
    c.lineStyle(4, 0xff00ff);
    c.drawCircle(0, 0, 24);
    c.endFill();

    const t = new Text(iconText, { fill: '#fff', fontSize: 20 });
    t.anchor.set(0.5);

    const container = new Container();
    container.addChild(c, t);

    container.eventMode = 'static';
    container.cursor = 'pointer';

    // Hover effect
    container.on('pointerover', () => {
      container.scale.set(1.1);
    });
    container.on('pointerout', () => {
      container.scale.set(1.0);
    });

    return container;
  }

  private setupInteractions(): void {
    this.infoButton.on('pointertap', () => {
      this.callbacks.onInfo?.();
    });

    this.helpButton.on('pointertap', () => {
      this.callbacks.onHelp?.();
    });

    this.settingsButton.on('pointertap', () => {
      this.callbacks.onSettings?.();
    });

    this.soundButton.on('pointertap', () => {
      this.soundEnabled = !this.soundEnabled;
      this.updateSoundIcon();
      this.callbacks.onSoundToggle?.();
    });

    this.featureButton.on('pointertap', () => {
      this.callbacks.onFeature?.();
    });
  }

  private updateSoundIcon(): void {
    const iconText = this.soundButton.children[1] as Text;
    if (iconText) {
      iconText.text = this.soundEnabled ? '🔊' : '🔇';
    }
  }

  public updateLayout(width?: number, height?: number, layout?: ReelLayout): void {
    if (width) this.screenWidth = width;
    if (height) this.screenHeight = height;
    if (layout) this.reelLayout = layout;

    this.y = 0;

    // Position logo centered above reels
    if (this.logo && this.reelLayout) {
      // Center horizontally on screen
      this.logo.x = this.screenWidth / 2;
      // Position above reels with some spacing (moved up slightly)
      this.logo.y = this.reelLayout.originY - 80;
    } else if (this.logo) {
      // Fallback: center on screen if no layout yet
      this.logo.x = this.screenWidth / 2;
      this.logo.y = 100;
    }

    // Position buttons (top right) - 16px spacing matching EXACT specification
    const buttonSpacing = 16;
    const rightEdge = this.screenWidth - 40;
    const buttonY = 24; // Center of 48px button

    // Rightmost button (sound)
    this.soundButton.x = rightEdge;
    this.soundButton.y = buttonY;

    // Fourth button (settings)
    this.settingsButton.x = rightEdge - (48 + buttonSpacing);
    this.settingsButton.y = buttonY;

    // Third button (help)
    this.helpButton.x = rightEdge - (48 + buttonSpacing) * 2;
    this.helpButton.y = buttonY;

    // Second button (info)
    this.infoButton.x = rightEdge - (48 + buttonSpacing) * 3;
    this.infoButton.y = buttonY;

    // Leftmost button (feature)
    this.featureButton.x = rightEdge - (48 + buttonSpacing) * 4;
    this.featureButton.y = buttonY;
  }
}
