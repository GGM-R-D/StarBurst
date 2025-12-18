import { Application } from 'pixi.js';

// Fixed game dimensions - game will always render at this size
const GAME_WIDTH = 1920;
const GAME_HEIGHT = 1080;

export async function createPixiApp(canvasId = 'game-canvas'): Promise<Application> {
  const canvas = document.getElementById(canvasId) as HTMLCanvasElement | null;
  if (!canvas) {
    throw new Error(`Canvas with id ${canvasId} not found`);
  }

  const app = new Application({
    view: canvas,
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
    antialias: true,
    autoDensity: true,
    backgroundColor: 0x000000,
    resolution: window.devicePixelRatio || 1
  });

  return app;
}

export { GAME_WIDTH, GAME_HEIGHT };

