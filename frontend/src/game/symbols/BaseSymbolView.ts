import { Container } from 'pixi.js';

export abstract class BaseSymbolView extends Container {
  readonly symbolId: number;

  constructor(symbolId: number) {
    super();
    this.symbolId = symbolId;
  }

  abstract playIdle(): void;
  abstract playWin(): void;
  abstract reset(): void;
}

