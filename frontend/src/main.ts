import { createPixiApp } from '@engine/renderer/PixiAppFactory';
import { GameApp } from '@game/GameApp';

async function bootstrap() {
  const app = await createPixiApp();
  const game = new GameApp(app);
  await game.init();
}

bootstrap().catch((err) => {
  console.error('Bootstrap error', err);
});

// Suppress browser extension errors (harmless, but noisy)
// These errors come from browser extensions and don't affect the game
window.addEventListener('error', (event) => {
  // Suppress common browser extension errors that are harmless
  const errorMessage = event.message || '';
  const errorSource = event.filename || '';
  
  if (
    errorMessage.includes('message channel closed') ||
    errorMessage.includes('Extension context invalidated') ||
    errorSource.includes('chrome-extension://') ||
    errorSource.includes('moz-extension://') ||
    errorSource.includes('safari-extension://')
  ) {
    event.preventDefault();
    event.stopPropagation();
    return false;
  }
}, true);

