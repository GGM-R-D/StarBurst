import { Modal } from './Modal';
import { Graphics, Text, TextStyle } from 'pixi.js';

export interface SettingsPopupCallbacks {
  onQualityChange?: (quality: 'low' | 'medium' | 'high') => void;
  onFullscreenToggle?: () => void;
}

/**
 * Settings popup with game settings options.
 */
export class SettingsPopup extends Modal {
  private callbacks: SettingsPopupCallbacks;
  private qualityButtons: Graphics[] = [];
  private qualityLabels: Text[] = [];
  private currentQuality: 'low' | 'medium' | 'high' = 'high';
  private fullscreenButton: Graphics;
  private fullscreenText: Text;
  private isFullscreen = false;

  constructor(callbacks: SettingsPopupCallbacks = {}) {
    super('Settings');
    this.callbacks = callbacks;

    // Quality settings
    this.addText('Graphics Quality', 0, 0, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    const qualities: Array<{ label: string; value: 'low' | 'medium' | 'high' }> = [
      { label: 'Low', value: 'low' },
      { label: 'Medium', value: 'medium' },
      { label: 'High', value: 'high' }
    ];

    qualities.forEach((quality, index) => {
      const button = new Graphics();
      const isSelected = quality.value === this.currentQuality;
      button.beginFill(isSelected ? 0xff00ff : 0x3d2817);
      button.lineStyle(2, isSelected ? 0xffd700 : 0x666666);
      button.drawRoundedRect(0, 0, 120, 40, 8);
      button.endFill();
      button.x = index * 140;
      button.y = 40;
      button.eventMode = 'static';
      button.cursor = 'pointer';

      const label = new Text(quality.label, {
        fontFamily: 'Arial',
        fontSize: 16,
        fontWeight: 'bold',
        fill: '#ffffff'
      });
      label.anchor.set(0.5);
      label.x = 60;
      label.y = 20;
      button.addChild(label);

      button.on('pointertap', () => {
        this.setQuality(quality.value);
      });

      this.qualityButtons.push(button);
      this.qualityLabels.push(label);
      this.contentContainer.addChild(button);
    });

    // Fullscreen toggle
    this.addText('', 0, 100); // Spacing

    this.addText('Display', 0, 120, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.fullscreenButton = new Graphics();
    this.fullscreenButton.beginFill(0x3d2817);
    this.fullscreenButton.lineStyle(2, 0xffd700);
    this.fullscreenButton.drawRoundedRect(0, 0, 150, 40, 8);
    this.fullscreenButton.endFill();
    this.fullscreenButton.x = 0;
    this.fullscreenButton.y = 160;
    this.fullscreenButton.eventMode = 'static';
    this.fullscreenButton.cursor = 'pointer';

    this.fullscreenText = new Text('Toggle Fullscreen', {
      fontFamily: 'Arial',
      fontSize: 16,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    this.fullscreenText.anchor.set(0.5);
    this.fullscreenText.x = 75;
    this.fullscreenText.y = 20;
    this.fullscreenButton.addChild(this.fullscreenText);

    this.fullscreenButton.on('pointertap', () => {
      this.toggleFullscreen();
    });

    this.contentContainer.addChild(this.fullscreenButton);

    // Additional info
    this.addText('', 0, 220); // Spacing

    this.addText('Note: Some settings may require a page refresh to take effect.', 0, 240, {
      fontSize: 14,
      fill: '#999999'
    });
  }

  private setQuality(quality: 'low' | 'medium' | 'high'): void {
    this.currentQuality = quality;
    this.updateQualityButtons();
    this.callbacks.onQualityChange?.(quality);
  }

  private updateQualityButtons(): void {
    const qualities: ('low' | 'medium' | 'high')[] = ['low', 'medium', 'high'];
    this.qualityButtons.forEach((button, index) => {
      const isSelected = qualities[index] === this.currentQuality;
      button.clear();
      button.beginFill(isSelected ? 0xff00ff : 0x3d2817);
      button.lineStyle(2, isSelected ? 0xffd700 : 0x666666);
      button.drawRoundedRect(0, 0, 120, 40, 8);
      button.endFill();
    });
  }

  private toggleFullscreen(): void {
    this.isFullscreen = !this.isFullscreen;
    this.callbacks.onFullscreenToggle?.();
    
    // Update button text
    this.fullscreenText.text = this.isFullscreen ? 'Exit Fullscreen' : 'Toggle Fullscreen';
  }
}

