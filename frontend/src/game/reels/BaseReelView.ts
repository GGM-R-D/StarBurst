import { Container } from 'pixi.js';
import type { BaseSymbolView } from '../symbols/BaseSymbolView';

export abstract class BaseReelView extends Container {
  protected symbols: BaseSymbolView[] = [];
  readonly reelIndex: number;

  constructor(reelIndex: number) {
    super();
    this.reelIndex = reelIndex;
  }

  abstract setStrip(symbolIds: number[]): void;
  abstract spin(): Promise<void>;
  abstract stopTo(resultSymbols: number[]): Promise<void>;
  abstract showWinOnRows(rows: number[]): void;
  abstract clearWinEffects(): void;
}

