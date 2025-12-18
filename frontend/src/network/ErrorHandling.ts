import { eventBus } from '@engine/events/EventBus';
import { GameEvent } from '@engine/events/events';

export function emitNetworkError(message: string): void {
  eventBus.emit(GameEvent.NETWORK_ERROR, { message });
}

export function emitFatalError(message: string): void {
  eventBus.emit(GameEvent.FATAL_ERROR, { message });
}

