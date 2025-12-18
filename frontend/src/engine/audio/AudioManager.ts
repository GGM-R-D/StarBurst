import { Howl, Howler } from 'howler';

export interface SoundConfig {
  src: string | string[];
  loop?: boolean;
  volume?: number;
  type?: 'music' | 'sfx';
}

export class AudioManager {
  private sounds = new Map<string, Howl>();
  private masterVolume: number = 1.0;
  private musicVolume: number = 0.8;
  private sfxVolume: number = 1.0;
  private soundEnabled: boolean = true;
  private currentMusic: string | null = null;

  setMasterVolume(volume: number): void {
    this.masterVolume = volume / 100;
    this.updateAllVolumes();
  }

  setMusicVolume(volume: number): void {
    this.musicVolume = volume / 100;
    this.updateMusicVolumes();
  }

  setSFXVolume(volume: number): void {
    this.sfxVolume = volume / 100;
    this.updateSFXVolumes();
  }

  setSoundEnabled(enabled: boolean): void {
    this.soundEnabled = enabled;
    if (!enabled) {
      Howler.mute(true);
    } else {
      Howler.mute(false);
      this.updateAllVolumes();
    }
  }

  private updateAllVolumes(): void {
    this.updateMusicVolumes();
    this.updateSFXVolumes();
  }

  private updateMusicVolumes(): void {
    this.sounds.forEach((sound, key) => {
      const config = (sound as any).__config as SoundConfig;
      if (config?.type === 'music') {
        const baseVolume = config.volume ?? 1.0;
        sound.volume(baseVolume * this.musicVolume * this.masterVolume);
      }
    });
  }

  private updateSFXVolumes(): void {
    this.sounds.forEach((sound, key) => {
      const config = (sound as any).__config as SoundConfig;
      if (config?.type === 'sfx' || !config?.type) {
        const baseVolume = config.volume ?? 1.0;
        sound.volume(baseVolume * this.sfxVolume * this.masterVolume);
      }
    });
  }

  registerSound(key: string, config: SoundConfig): void {
    if (this.sounds.has(key)) return;
    
    const sound = new Howl({
      src: Array.isArray(config.src) ? config.src : [config.src],
      loop: config.loop ?? false,
      volume: config.volume ?? 1.0
    });

    // Store config for volume updates
    (sound as any).__config = config;

    // Set initial volume based on type
    if (config.type === 'music') {
      sound.volume((config.volume ?? 1.0) * this.musicVolume * this.masterVolume);
    } else {
      sound.volume((config.volume ?? 1.0) * this.sfxVolume * this.masterVolume);
    }

    this.sounds.set(key, sound);
  }

  play(key: string, force: boolean = false): void {
    if (!this.soundEnabled && !force) {
      console.warn(`[AudioManager] Sound disabled, skipping: ${key}`);
      return;
    }
    
    const sound = this.sounds.get(key);
    if (sound) {
      const soundId = sound.play();
      if (soundId === undefined) {
        console.warn(`[AudioManager] Failed to play sound: ${key}`);
      } else {
        console.debug(`[AudioManager] Playing sound: ${key} (id: ${soundId})`);
      }
    } else {
      console.warn(`[AudioManager] Sound not found: ${key}. Registered sounds:`, Array.from(this.sounds.keys()));
    }
  }

  stop(key: string): void {
    const sound = this.sounds.get(key);
    sound?.stop();
  }

  stopAll(): void {
    this.sounds.forEach((sound) => {
      sound.stop();
    });
  }

  playMusic(key: string): void {
    // Stop current music if playing
    if (this.currentMusic && this.currentMusic !== key) {
      this.stop(this.currentMusic);
    }
    this.currentMusic = key;
    this.play(key);
  }

  stopMusic(): void {
    if (this.currentMusic) {
      this.stop(this.currentMusic);
      this.currentMusic = null;
    }
  }

  getMasterVolume(): number {
    return this.masterVolume * 100;
  }

  getMusicVolume(): number {
    return this.musicVolume * 100;
  }

  getSFXVolume(): number {
    return this.sfxVolume * 100;
  }

  isSoundEnabled(): boolean {
    return this.soundEnabled;
  }
}

