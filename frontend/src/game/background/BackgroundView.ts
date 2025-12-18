import { Sprite, Texture, Container, Graphics, WRAP_MODES } from 'pixi.js';
import { backgroundTextureUrl } from '@game/config/assetsManifest';

export class BackgroundView extends Container {
  private sprite: Sprite | Graphics | null = null;
  private isFallback = false;
  private pendingWidth: number = 0;
  private pendingHeight: number = 0;

  constructor() {
    super();
    const texture = Texture.from(backgroundTextureUrl);
    
    // Prevent texture from repeating/tiling
    if (texture.baseTexture) {
      texture.baseTexture.wrapMode = WRAP_MODES.CLAMP;
    }
    
    this.sprite = new Sprite(texture);
    this.sprite.anchor.set(0.5, 0.5);
    this.addChild(this.sprite);
    
    // Handle texture loading
    if (texture.valid) {
      // Texture already loaded, we can resize immediately if dimensions are available
      // This will be handled by the first resize() call
    } else {
      // Wait for texture to load
      texture.baseTexture.once('loaded', () => {
        if (this.pendingWidth > 0 && this.pendingHeight > 0) {
          this.resize(this.pendingWidth, this.pendingHeight);
        }
      });
      
      texture.on('error', () => {
        this.createFallbackBackground();
      });
    }
  }

  private createFallbackBackground(): void {
    if (this.isFallback) return;
    
    if (this.sprite) {
      this.removeChild(this.sprite);
    }
    
    // Create cosmic purple-pink gradient background matching Starburst exactly
    const graphics = new Graphics();
    
    // Base deep purple-pink gradient (nebula effect)
    graphics.beginFill(0x2d1b3d); // Deep purple base
    graphics.drawRect(0, 0, 1920, 1080);
    graphics.endFill();
    
    // Add gradient layers for cosmic effect
    graphics.beginFill(0x4a1d7d, 0.6); // Purple layer
    graphics.drawRect(0, 0, 1920, 1080);
    graphics.endFill();
    
    graphics.beginFill(0x6b1d7c, 0.4); // Pink-purple layer
    graphics.drawRect(0, 0, 1920, 1080);
    graphics.endFill();
    
    // Reddish-orange planet in upper right
    graphics.beginFill(0xff6b35, 0.7);
    graphics.drawCircle(1600, 200, 180);
    graphics.endFill();
    graphics.beginFill(0xff8c5a, 0.5);
    graphics.drawCircle(1600, 200, 150);
    graphics.endFill();
    
    // Pinkish planet in lower left
    graphics.beginFill(0xff6b9d, 0.6);
    graphics.drawCircle(200, 900, 200);
    graphics.endFill();
    graphics.beginFill(0xff8fb8, 0.4);
    graphics.drawCircle(200, 900, 170);
    graphics.endFill();
    
    // Add some star glints
    for (let i = 0; i < 50; i++) {
      const x = Math.random() * 1920;
      const y = Math.random() * 1080;
      const size = Math.random() * 3 + 1;
      graphics.beginFill(0xffffff, 0.8);
      graphics.drawCircle(x, y, size);
      graphics.endFill();
    }
    
    this.sprite = graphics;
    this.addChild(graphics);
    this.isFallback = true;
  }

  resize(width: number, height: number): void {
    this.pendingWidth = width;
    this.pendingHeight = height;
    
    if (!this.sprite) {
      return;
    }

    if (this.isFallback && this.sprite instanceof Graphics) {
      // Resize fallback graphics to fill screen with cosmic background
      this.sprite.clear();
      
      // Base deep purple-pink gradient
      this.sprite.beginFill(0x2d1b3d);
      this.sprite.drawRect(0, 0, width, height);
      this.sprite.endFill();
      
      // Add gradient layers
      this.sprite.beginFill(0x4a1d7d, 0.6);
      this.sprite.drawRect(0, 0, width, height);
      this.sprite.endFill();
      
      this.sprite.beginFill(0x6b1d7c, 0.4);
      this.sprite.drawRect(0, 0, width, height);
      this.sprite.endFill();
      
      // Reddish-orange planet in upper right
      const planet1X = width * 0.83;
      const planet1Y = height * 0.19;
      const planet1Size = width * 0.094;
      this.sprite.beginFill(0xff6b35, 0.7);
      this.sprite.drawCircle(planet1X, planet1Y, planet1Size);
      this.sprite.endFill();
      this.sprite.beginFill(0xff8c5a, 0.5);
      this.sprite.drawCircle(planet1X, planet1Y, planet1Size * 0.83);
      this.sprite.endFill();
      
      // Pinkish planet in lower left
      const planet2X = width * 0.104;
      const planet2Y = height * 0.83;
      const planet2Size = width * 0.104;
      this.sprite.beginFill(0xff6b9d, 0.6);
      this.sprite.drawCircle(planet2X, planet2Y, planet2Size);
      this.sprite.endFill();
      this.sprite.beginFill(0xff8fb8, 0.4);
      this.sprite.drawCircle(planet2X, planet2Y, planet2Size * 0.85);
      this.sprite.endFill();
      
      // Add star glints
      for (let i = 0; i < 50; i++) {
        const x = Math.random() * width;
        const y = Math.random() * height;
        const size = Math.random() * 3 + 1;
        this.sprite.beginFill(0xffffff, 0.8);
        this.sprite.drawCircle(x, y, size);
        this.sprite.endFill();
      }
    } else if (this.sprite instanceof Sprite) {
      const texture = this.sprite.texture;
      
      // Ensure texture is loaded and valid
      if (!texture.valid) {
        // Wait for texture to load
        texture.baseTexture.once('loaded', () => {
          this.resize(width, height);
        });
        return;
      }
      
      const texW = texture.width;
      const texH = texture.height;
      
      // Ensure we have valid texture dimensions
      if (!texW || !texH || texW <= 0 || texH <= 0) {
        return;
      }
      
      // "Cover" style scaling: fill screen, preserving aspect ratio
      const scaleX = width / texW;
      const scaleY = height / texH;
      const scale = Math.max(scaleX, scaleY);
      
      // Apply scale
      this.sprite.scale.set(scale);
      
      // Ensure anchor is centered
      this.sprite.anchor.set(0.5, 0.5);
      
      // Position at center of screen
      this.sprite.position.set(width / 2, height / 2);
    }
  }
}

