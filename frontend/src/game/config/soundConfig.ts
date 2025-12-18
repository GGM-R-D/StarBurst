import type { SoundConfig } from '@engine/audio/AudioManager';
import { getAssetUrl } from './assetsManifest';

/**
 * Sound configuration mapping for all game sounds.
 */
export const SOUND_CONFIG: Record<string, SoundConfig> = {
  // Music
  'background-music': {
    src: getAssetUrl('/sounds/background-music.mp3'),
    loop: true,
    volume: 0.5,
    type: 'music'
  },
  'free-spins-music': {
    src: getAssetUrl('/sounds/free-spins-music.mp3'),
    loop: true,
    volume: 0.7,
    type: 'music'
  },

  // Sound Effects
  'spin': {
    src: getAssetUrl('/sounds/spin.mp3'),
    loop: true,
    volume: 0.9,
    type: 'sfx'
  },
  'stop': {
    src: getAssetUrl('/sounds/stop.mp3'),
    volume: 0.9,
    type: 'sfx'
  },
  'win': {
    src: getAssetUrl('/sounds/win.wav'),
    volume: 0.2,
    type: 'sfx'
  },
  'big-win': {
    src: getAssetUrl('/sounds/big-win.wav'),
    volume: 0.2,
    type: 'sfx'
  },
  'click': {
    src: getAssetUrl('/sounds/click.wav'),
    volume: 0.7,
    type: 'sfx'
  },
  'expanding': {
    src: getAssetUrl('/sounds/Expanding.mp3'),
    volume: 0.9,
    type: 'sfx'
  }
};

