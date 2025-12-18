import { Modal } from './Modal';
import { Graphics, Text } from 'pixi.js';

export interface SoundPopupCallbacks {
  onMasterVolumeChange?: (volume: number) => void;
  onMusicVolumeChange?: (volume: number) => void;
  onSFXVolumeChange?: (volume: number) => void;
  onSoundToggle?: (enabled: boolean) => void;
}

/**
 * Sound popup with audio controls.
 */
export class SoundPopup extends Modal {
  private callbacks: SoundPopupCallbacks;
  private masterVolume: number = 100;
  private musicVolume: number = 80;
  private sfxVolume: number = 100;
  private soundEnabled: boolean = true;

  private masterVolumeText: Text;
  private musicVolumeText: Text;
  private sfxVolumeText: Text;
  private soundToggleButton: Graphics;
  private soundToggleText: Text;

  constructor(callbacks: SoundPopupCallbacks = {}) {
    super('Sound Settings');
    this.callbacks = callbacks;

    // Master volume
    this.addText('Master Volume', 0, 0, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.masterVolumeText = this.addText('100%', 400, 0, {
      fontSize: 18,
      fill: '#ffffff'
    });

    const masterControls = this.createVolumeControls(0, 40, this.masterVolume, (volume) => {
      this.masterVolume = volume;
      this.masterVolumeText.text = `${volume}%`;
      this.callbacks.onMasterVolumeChange?.(volume);
    });
    this.contentContainer.addChild(masterControls);

    // Music volume
    this.addText('Music Volume', 0, 100, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.musicVolumeText = this.addText('80%', 400, 100, {
      fontSize: 18,
      fill: '#ffffff'
    });

    const musicControls = this.createVolumeControls(0, 140, this.musicVolume, (volume) => {
      this.musicVolume = volume;
      this.musicVolumeText.text = `${volume}%`;
      this.callbacks.onMusicVolumeChange?.(volume);
    });
    this.contentContainer.addChild(musicControls);

    // SFX volume
    this.addText('SFX Volume', 0, 200, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.sfxVolumeText = this.addText('100%', 400, 200, {
      fontSize: 18,
      fill: '#ffffff'
    });

    const sfxControls = this.createVolumeControls(0, 240, this.sfxVolume, (volume) => {
      this.sfxVolume = volume;
      this.sfxVolumeText.text = `${volume}%`;
      this.callbacks.onSFXVolumeChange?.(volume);
    });
    this.contentContainer.addChild(sfxControls);

    // Sound toggle
    this.addText('', 0, 300); // Spacing

    this.soundToggleButton = new Graphics();
    this.soundToggleButton.beginFill(this.soundEnabled ? 0x00cc00 : 0x3d2817);
    this.soundToggleButton.lineStyle(2, 0xffd700);
    this.soundToggleButton.drawRoundedRect(0, 0, 200, 50, 10);
    this.soundToggleButton.endFill();
    this.soundToggleButton.x = 0;
    this.soundToggleButton.y = 320;
    this.soundToggleButton.eventMode = 'static';
    this.soundToggleButton.cursor = 'pointer';

    this.soundToggleText = new Text(this.soundEnabled ? 'Sound: ON' : 'Sound: OFF', {
      fontFamily: 'Arial',
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    this.soundToggleText.anchor.set(0.5);
    this.soundToggleText.x = 100;
    this.soundToggleText.y = 25;
    this.soundToggleButton.addChild(this.soundToggleText);

    this.soundToggleButton.on('pointertap', () => {
      this.toggleSound();
    });

    this.contentContainer.addChild(this.soundToggleButton);
  }

  private createVolumeControls(x: number, y: number, initialVolume: number, onChange: (volume: number) => void): Graphics {
    const container = new Graphics();
    container.x = x;
    container.y = y;

    // Volume bar background
    container.beginFill(0x333333);
    container.drawRoundedRect(0, 0, 300, 30, 5);
    container.endFill();

    // Volume bar fill
    const fillWidth = (initialVolume / 100) * 300;
    container.beginFill(0xff00ff);
    container.drawRoundedRect(0, 0, fillWidth, 30, 5);
    container.endFill();

    // Minus button
    const minusBtn = new Graphics();
    minusBtn.beginFill(0x3d2817);
    minusBtn.lineStyle(2, 0xffd700);
    minusBtn.drawRoundedRect(0, 0, 40, 30, 5);
    minusBtn.endFill();
    minusBtn.x = 310;
    minusBtn.y = 0;
    minusBtn.eventMode = 'static';
    minusBtn.cursor = 'pointer';

    const minusText = new Text('-', {
      fontFamily: 'Arial',
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    minusText.anchor.set(0.5);
    minusText.x = 20;
    minusText.y = 15;
    minusBtn.addChild(minusText);

    minusBtn.on('pointertap', () => {
      const newVolume = Math.max(0, initialVolume - 10);
      onChange(newVolume);
      this.updateVolumeBar(container, newVolume);
    });

    // Plus button
    const plusBtn = new Graphics();
    plusBtn.beginFill(0x3d2817);
    plusBtn.lineStyle(2, 0xffd700);
    plusBtn.drawRoundedRect(0, 0, 40, 30, 5);
    plusBtn.endFill();
    plusBtn.x = 360;
    plusBtn.y = 0;
    plusBtn.eventMode = 'static';
    plusBtn.cursor = 'pointer';

    const plusText = new Text('+', {
      fontFamily: 'Arial',
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    plusText.anchor.set(0.5);
    plusText.x = 20;
    plusText.y = 15;
    plusBtn.addChild(plusText);

    plusBtn.on('pointertap', () => {
      const newVolume = Math.min(100, initialVolume + 10);
      onChange(newVolume);
      this.updateVolumeBar(container, newVolume);
    });

    container.addChild(minusBtn);
    container.addChild(plusBtn);

    return container;
  }

  private updateVolumeBar(container: Graphics, volume: number): void {
    // Save buttons before clearing
    const minusBtn = container.children[0] as Graphics;
    const plusBtn = container.children[1] as Graphics;
    
    // Clear and redraw
    container.clear();
    
    // Background
    container.beginFill(0x333333);
    container.drawRoundedRect(0, 0, 300, 30, 5);
    container.endFill();

    // Fill
    const fillWidth = (volume / 100) * 300;
    container.beginFill(0xff00ff);
    container.drawRoundedRect(0, 0, fillWidth, 30, 5);
    container.endFill();

    // Re-add buttons
    if (minusBtn) {
      container.addChild(minusBtn);
    }
    if (plusBtn) {
      container.addChild(plusBtn);
    }
  }

  private toggleSound(): void {
    this.soundEnabled = !this.soundEnabled;
    this.soundToggleButton.clear();
    this.soundToggleButton.beginFill(this.soundEnabled ? 0x00cc00 : 0x3d2817);
    this.soundToggleButton.lineStyle(2, 0xffd700);
    this.soundToggleButton.drawRoundedRect(0, 0, 200, 50, 10);
    this.soundToggleButton.endFill();
    this.soundToggleText.text = this.soundEnabled ? 'Sound: ON' : 'Sound: OFF';
    this.callbacks.onSoundToggle?.(this.soundEnabled);
  }
}

