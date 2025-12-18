import { Modal } from './Modal';
import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import type { Application } from 'pixi.js';

export interface AutoSpinPopupCallbacks {
  onAutoSpinStart?: (count: number) => void;
}

/**
 * Auto spin popup allowing player to select number of auto spins.
 */
export class AutoSpinPopup extends Modal {
  private callbacks: AutoSpinPopupCallbacks;
  private selectedCount: number = 10;
  private countText: Text;
  private spinCountButtons: Graphics[] = [];

  constructor(callbacks: AutoSpinPopupCallbacks = {}, app?: Application) {
    super('Auto Spin', false, app);
    this.callbacks = callbacks;

    // Title text
    const titleText = this.addText('Select Number of Spins', 0, 20, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });
    titleText.x = 300 - titleText.width / 2; // Center

    // Display selected count
    this.countText = new Text('10', {
      fontSize: 48,
      fontWeight: 'bold',
      fill: '#ffffff',
      fontFamily: 'Arial'
    });
    this.countText.anchor.set(0.5, 0.5);
    this.countText.x = 300; // Center of panel
    this.countText.y = 100;
    this.contentContainer.addChild(this.countText);

    // Create preset buttons for common values
    const presets = [5, 10, 25, 50, 100];
    const buttonWidth = 80;
    const buttonHeight = 50;
    const buttonSpacing = 20;
    const totalWidth = presets.length * buttonWidth + (presets.length - 1) * buttonSpacing;
    const startX = (600 - totalWidth) / 2;

    presets.forEach((count, index) => {
      const button = this.createCountButton(count.toString(), buttonWidth, buttonHeight);
      button.x = startX + index * (buttonWidth + buttonSpacing);
      button.y = 150;
      button.eventMode = 'static';
      button.cursor = 'pointer';

      button.on('pointertap', () => {
        this.selectedCount = count;
        this.updateCountDisplay();
        this.updateButtonStates();
      });

      button.on('pointerover', () => {
        if (this.selectedCount !== count) {
          button.scale.set(1.05);
        }
      });

      button.on('pointerout', () => {
        button.scale.set(1.0);
      });

      this.spinCountButtons.push(button);
      this.contentContainer.addChild(button);
    });

    // Custom input area (optional - for now just use presets)
    const customLabel = this.addText('Or enter custom amount:', 0, 230, {
      fontSize: 16,
      fill: '#cccccc'
    });
    customLabel.x = 300 - customLabel.width / 2; // Center

    // Create custom input buttons
    const customControls = this.createCustomControls();
    customControls.y = 270;
    this.contentContainer.addChild(customControls);

    // Start button
    const startButton = this.createStartButton();
    startButton.x = 300; // Center
    startButton.y = 360;
    startButton.eventMode = 'static';
    startButton.cursor = 'pointer';

    startButton.on('pointertap', () => {
      this.callbacks.onAutoSpinStart?.(this.selectedCount);
      this.close();
    });

    startButton.on('pointerover', () => {
      startButton.scale.set(1.05);
    });

    startButton.on('pointerout', () => {
      startButton.scale.set(1.0);
    });

    this.contentContainer.addChild(startButton);

    // Initialize button states
    this.updateButtonStates();
  }

  private createCountButton(label: string, width: number, height: number): Graphics {
    const button = new Graphics();
    
    // Background
    button.beginFill(0x3d2817, 0.95);
    button.lineStyle(2, 0xffd700, 1);
    button.drawRoundedRect(0, 0, width, height, 8);
    button.endFill();

    // Label
    const text = new Text(label, {
      fill: 0xffffff,
      fontSize: 18,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    text.anchor.set(0.5);
    text.x = width / 2;
    text.y = height / 2;
    button.addChild(text);

    return button;
  }

  private createCustomControls(): Container {
    const container = new Container();

    // Decrease button
    const decreaseBtn = new Graphics();
    decreaseBtn.beginFill(0x3d2817, 0.95);
    decreaseBtn.lineStyle(2, 0xffd700, 1);
    decreaseBtn.drawRoundedRect(0, 0, 60, 40, 8);
    decreaseBtn.endFill();
    
    const minusText = new Text('-', {
      fill: 0xffffff,
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    minusText.anchor.set(0.5);
    minusText.x = 30;
    minusText.y = 20;
    decreaseBtn.addChild(minusText);
    decreaseBtn.eventMode = 'static';
    decreaseBtn.cursor = 'pointer';

    decreaseBtn.on('pointertap', () => {
      this.selectedCount = Math.max(1, this.selectedCount - 1);
      this.updateCountDisplay();
      this.updateButtonStates();
    });

    decreaseBtn.on('pointerover', () => decreaseBtn.scale.set(1.1));
    decreaseBtn.on('pointerout', () => decreaseBtn.scale.set(1.0));

    // Increase button
    const increaseBtn = new Graphics();
    increaseBtn.beginFill(0x3d2817, 0.95);
    increaseBtn.lineStyle(2, 0xffd700, 1);
    increaseBtn.drawRoundedRect(0, 0, 60, 40, 8);
    increaseBtn.endFill();
    
    const plusText = new Text('+', {
      fill: 0xffffff,
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    plusText.anchor.set(0.5);
    plusText.x = 30;
    plusText.y = 20;
    increaseBtn.addChild(plusText);
    increaseBtn.eventMode = 'static';
    increaseBtn.cursor = 'pointer';

    increaseBtn.on('pointertap', () => {
      this.selectedCount = Math.min(999, this.selectedCount + 1);
      this.updateCountDisplay();
      this.updateButtonStates();
    });

    increaseBtn.on('pointerover', () => increaseBtn.scale.set(1.1));
    increaseBtn.on('pointerout', () => increaseBtn.scale.set(1.0));

    // Position buttons
    decreaseBtn.x = 200;
    increaseBtn.x = 340;

    container.addChild(decreaseBtn);
    container.addChild(increaseBtn);

    return container;
  }

  private createStartButton(): Graphics {
    const button = new Graphics();
    const width = 200;
    const height = 50;

    // Background with glow
    button.beginFill(0x00ff00, 0.8);
    button.lineStyle(3, 0x00cc00, 1);
    button.drawRoundedRect(0, 0, width, height, 12);
    button.endFill();

    // Text
    const text = new Text('START', {
      fill: 0xffffff,
      fontSize: 24,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      stroke: 0x000000,
      strokeThickness: 2
    });
    text.anchor.set(0.5);
    text.x = width / 2;
    text.y = height / 2;
    button.addChild(text);

    return button;
  }

  private updateCountDisplay(): void {
    this.countText.text = this.selectedCount.toString();
  }

  private updateButtonStates(): void {
    // Update preset button appearances based on selection
    const presets = [5, 10, 25, 50, 100];
    this.spinCountButtons.forEach((button, index) => {
      const count = presets[index];
      const isSelected = this.selectedCount === count;
      
      button.clear();
      if (isSelected) {
        // Highlight selected button
        button.beginFill(0x5b1b7e, 0.95);
        button.lineStyle(3, 0xff00ff, 1);
      } else {
        button.beginFill(0x3d2817, 0.95);
        button.lineStyle(2, 0xffd700, 1);
      }
      button.drawRoundedRect(0, 0, 80, 50, 8);
      button.endFill();
    });
  }

  public updateLayout(width: number, height: number): void {
    super.updateLayout(width, height);
    // Reposition count text to center
    this.countText.x = 300;
  }
}

