import type { PlayResponse } from '@network/types';

export class WildManager {
  lockWilds(result: PlayResponse): void {
    // TODO: implement expanding wild visual logic
    console.info('WildManager lockWilds', result.wildPositions);
  }

  reset(): void {
    // TODO: clear wild states
  }
}

