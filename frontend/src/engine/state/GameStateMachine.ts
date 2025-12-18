import { GameState } from './GameState';

export abstract class GameStateMachine {
  private _state: GameState = GameState.BOOT;

  get state(): GameState {
    return this._state;
  }

  protected setState(next: GameState): void {
    if (this._state === next) return;
    const prev = this._state;
    this._state = next;
    this.onStateChanged(prev, next);
  }

  protected abstract onStateChanged(prev: GameState, next: GameState): void;
}

