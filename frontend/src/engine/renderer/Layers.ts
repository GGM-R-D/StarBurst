import { Container } from 'pixi.js';

export interface GameLayers {
  background: Container;
  reels: Container;
  fx: Container;
  ui: Container;
  modals: Container;
}

export function createLayers(root: Container): GameLayers {
  const background = new Container();
  const reels = new Container();
  const fx = new Container();
  const ui = new Container();
  const modals = new Container();

  root.addChild(background, reels, fx, ui, modals);

  return { background, reels, fx, ui, modals };
}

