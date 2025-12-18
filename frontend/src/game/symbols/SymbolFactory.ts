import { AnimatedSprite, Texture, BaseTexture } from 'pixi.js';
import { symbolAnimations, SymbolAnimationConfig } from '@game/config/assetsManifest';

const symbolConfigById: Record<string, SymbolAnimationConfig> = {};
const textureCache: Map<string, Texture> = new Map();

// Initialize lookup table
symbolAnimations.forEach((config) => {
  symbolConfigById[config.id] = config;
});

// Preload all symbol textures
export async function preloadSymbolTextures(): Promise<void> {
  const loadPromises: Promise<void>[] = [];
  
  symbolAnimations.forEach((config) => {
    config.frames.forEach((url) => {
      if (!textureCache.has(url)) {
        const promise = new Promise<void>((resolve, reject) => {
          const texture = Texture.from(url);
          textureCache.set(url, texture);
          
          if (texture.baseTexture.valid) {
            resolve();
          } else {
            texture.baseTexture.on('loaded', () => resolve());
            texture.baseTexture.on('error', () => {
              console.warn(`Failed to preload texture: ${url}`);
              resolve(); // Continue even if one fails
            });
          }
        });
        loadPromises.push(promise);
      }
    });
  });
  
  await Promise.all(loadPromises);
}

// Create a fallback texture for missing assets
// Use a fully transparent texture so we never see a solid color block on screen.
const createFallbackTexture = (): Texture => {
  const canvas = document.createElement('canvas');
  canvas.width = 100;
  canvas.height = 100;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.clearRect(0, 0, 100, 100);
  }
  return Texture.from(canvas);
};

export function createSymbolSprite(symbolId: string): AnimatedSprite {
  const config = symbolConfigById[symbolId];

  if (!config) {
    // Return placeholder if config not found
    const placeholder = new AnimatedSprite([createFallbackTexture()]);
    placeholder.anchor.set(0.5);
    placeholder.visible = true;
    return placeholder;
  }

  // Map frame URLs to textures - use cached textures if available
  const textures = config.frames.map((url) => {
    try {
      // Use cached texture if available, otherwise create new one
      let texture = textureCache.get(url);
      if (!texture) {
        texture = Texture.from(url);
        textureCache.set(url, texture);
      }
      
      // Handle texture loading errors
      if (!texture.baseTexture.valid) {
        texture.baseTexture.on('error', () => {
          console.warn(`Failed to load texture: ${url}`);
        });
      }
      
      return texture;
    } catch (error) {
      console.warn(`Error creating texture from ${url}:`, error);
      return createFallbackTexture();
    }
  });

  // Create AnimatedSprite with textures
  const sprite = new AnimatedSprite(textures);

  // Set anchor to center
  sprite.anchor.set(0.5);

  // Set animation speed (default 0.5 if not specified)
  sprite.animationSpeed = config.animationSpeed ?? 0.5;

  // Set loop (default true if not specified)
  sprite.loop = config.loop ?? true;

  // CRITICAL: Make sprite visible immediately
  sprite.visible = true;
  
  // Start playing animation
  sprite.play();

  // If textures are already loaded, sprite will show immediately
  // If textures are still loading, sprite will appear once they load
  // Check if textures are ready and force update if needed
  const checkAndUpdate = () => {
    if (textures.some(tex => tex.baseTexture && tex.baseTexture.valid)) {
      // At least one texture is valid, ensure sprite is visible
      sprite.visible = true;
    }
  };
  
  // Check immediately
  checkAndUpdate();
  
  // Also check when textures load
  textures.forEach(texture => {
    if (texture.baseTexture) {
      if (texture.baseTexture.valid) {
        checkAndUpdate();
      } else {
        texture.baseTexture.once('loaded', checkAndUpdate);
      }
    }
  });

  return sprite;
}
