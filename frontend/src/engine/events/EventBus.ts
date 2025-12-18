import EventEmitter from 'eventemitter3';
import { GameEvent } from './events';

export type EventPayloads = {
  [GameEvent.SPIN_RESULT]: unknown;
  [GameEvent.RESPIN_RESULT]: unknown;
  [GameEvent.BALANCE_UPDATED]: { balance: number };
  [GameEvent.NETWORK_ERROR]: { message: string };
  [GameEvent.FATAL_ERROR]: { message: string };
  [key: string]: any;
};

class EventBus {
  private emitter = new EventEmitter();

  on<K extends keyof EventPayloads>(
    event: K,
    listener: (payload: EventPayloads[K]) => void
  ): void {
    this.emitter.on(event, listener as any);
  }

  off<K extends keyof EventPayloads>(
    event: K,
    listener: (payload: EventPayloads[K]) => void
  ): void {
    this.emitter.off(event, listener as any);
  }

  emit<K extends keyof EventPayloads>(event: K, payload: EventPayloads[K]): void {
    this.emitter.emit(event, payload);
  }
}

export const eventBus = new EventBus();

