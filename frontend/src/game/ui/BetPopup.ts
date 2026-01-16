import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import { Modal } from './Modal';
import { roundToTwoDecimals, getNextBetAmount, getPreviousBetAmount, getBetLevels } from '@game/utils/betUtils';

export interface BetPopupCallbacks {
  onBetSelected?: (bet: number) => void;
  onClose?: () => void;
}

const NUM_LINES = 10; // Starburst has 10 fixed paylines

/**
 * Bet selection popup with simple BET selector.
 * Shows single BET value with +/- controls.
 */
export class BetPopup extends Modal {
  private callbacks: BetPopupCallbacks;
  private selectedTotalBet: number = 1.0;
  
  // UI elements
  private betValueText: Text;
  private betMaxButton: Graphics;
  private betMaxText: Text;
  
  // Buttons
  private betMinusButton: Graphics;
  private betPlusButton: Graphics;
  
  // Bet button position for relative positioning
  private betButtonPosition: { x: number; y: number; width: number; height: number } | null = null;

  constructor(callbacks: BetPopupCallbacks = {}, currentBet: number = 1.0) {
    super('BETTING ON 10 LINES', false);
    this.callbacks = callbacks;
    this.selectedTotalBet = roundToTwoDecimals(currentBet);
    
    this.setupUI();
  }

  private setupUI(): void {
    const panelWidth = 400;
    const panelHeight = 300;
    
    // Update panel size
    this.panel.clear();
    this.panel.beginFill(0x1a1a1a, 0.95);
    this.panel.lineStyle(2, 0x666666, 1);
    this.panel.drawRoundedRect(0, 0, panelWidth, panelHeight, 15);
    this.panel.endFill();
    
    // Update title color to orange-gold
    this.titleText.style.fill = '#ffaa00';
    this.titleText.x = panelWidth / 2;
    
    // Update close button to X icon style (white X)
    this.closeButton.clear();
    this.closeButton.beginFill(0x000000, 0);
    this.closeButton.lineStyle(0, 0x000000, 0);
    this.closeButton.drawRect(0, 0, 40, 40);
    this.closeButton.endFill();
    this.closeButton.x = panelWidth - 50;
    this.closeButton.y = 10;
    
    this.closeButtonText.text = '✕';
    this.closeButtonText.style.fontSize = 32;
    this.closeButtonText.style.fill = '#ffffff';
    this.closeButtonText.anchor.set(0.5);
    this.closeButtonText.x = 20;
    this.closeButtonText.y = 20;
    
    // BET section (single section)
    const startY = 100;
    this.createBetSection(startY);
    
    // BET MAX button
    this.createBetMaxButton(startY + 120);
  }
  
  private createBetSection(y: number): void {
    const sectionWidth = 350;
    const sectionHeight = 80;
    const sectionX = (400 - sectionWidth) / 2;
    
    // Create three-section layout: minus | BET value | plus
    const sectionPartWidth = sectionWidth / 3;
    
    // Minus button (left section)
    this.betMinusButton = this.createControlButton('-', sectionX + sectionPartWidth / 2, y + sectionHeight / 2, false);
    this.betMinusButton.on('pointertap', () => {
      const newBet = getPreviousBetAmount(this.selectedTotalBet, 0.1);
      this.updateBet(newBet);
    });
    this.contentContainer.addChild(this.betMinusButton);
    
    // BET label and value (center section)
    const labelText = new Text('BET', {
      fontFamily: 'Arial',
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffaa00'
    });
    labelText.anchor.set(0.5, 0.5);
    labelText.x = sectionX + sectionWidth / 2;
    labelText.y = y + 20;
    this.contentContainer.addChild(labelText);
    
    this.betValueText = new Text(this.selectedTotalBet.toString(), {
      fontFamily: 'Arial',
      fontSize: 36,
      fontWeight: 'bold',
      fill: '#ffaa00'
    });
    this.betValueText.anchor.set(0.5, 0.5);
    this.betValueText.x = sectionX + sectionWidth / 2;
    this.betValueText.y = y + 55;
    this.contentContainer.addChild(this.betValueText);
    
    // Plus button (right section)
    this.betPlusButton = this.createControlButton('+', sectionX + sectionWidth - sectionPartWidth / 2, y + sectionHeight / 2, true);
    this.betPlusButton.on('pointertap', () => {
      const betLevels = getBetLevels();
      const maxBet = betLevels[betLevels.length - 1];
      const newBet = getNextBetAmount(this.selectedTotalBet, maxBet);
      this.updateBet(newBet);
    });
    this.contentContainer.addChild(this.betPlusButton);
  }
  
  private updateBet(newBet: number): void {
    this.selectedTotalBet = roundToTwoDecimals(newBet);
    if (this.betValueText) {
      if (this.selectedTotalBet % 1 === 0) {
        this.betValueText.text = this.selectedTotalBet.toString();
      } else {
        this.betValueText.text = this.selectedTotalBet.toFixed(2);
      }
    }
  }


  private createControlButton(symbol: string, x: number, y: number, isPlus: boolean): Graphics {
    const button = new Graphics();
    const radius = 25;
    
    if (isPlus) {
      // Green circle for plus
      button.beginFill(0x00aa00, 1);
      button.lineStyle(2, 0x00cc00, 1);
    } else {
      // White circle for minus
      button.beginFill(0xffffff, 1);
      button.lineStyle(2, 0xcccccc, 1);
    }
    button.drawCircle(0, 0, radius);
    button.endFill();
    button.x = x;
    button.y = y;
    button.eventMode = 'static';
    button.cursor = 'pointer';
    
    // Symbol text
    const symbolText = new Text(symbol, {
      fontFamily: 'Arial',
      fontSize: 28,
      fontWeight: 'bold',
      fill: isPlus ? '#ffffff' : '#000000'
    });
    symbolText.anchor.set(0.5);
    symbolText.x = 0;
    symbolText.y = 0;
    button.addChild(symbolText);
    
    // Hover effect
    button.on('pointerover', () => {
      button.scale.set(1.1);
    });
    button.on('pointerout', () => {
      button.scale.set(1.0);
    });
    
    return button;
  }

  private createBetMaxButton(y: number): void {
    const buttonWidth = 200;
    const buttonHeight = 40;
    const buttonX = (400 - buttonWidth) / 2;
    
    this.betMaxButton = new Graphics();
    this.betMaxButton.beginFill(0x00aa00, 1);
    this.betMaxButton.lineStyle(2, 0x00cc00, 1);
    this.betMaxButton.drawRoundedRect(0, 0, buttonWidth, buttonHeight, 8);
    this.betMaxButton.endFill();
    this.betMaxButton.x = buttonX;
    this.betMaxButton.y = y;
    this.betMaxButton.eventMode = 'static';
    this.betMaxButton.cursor = 'pointer';
    
    this.betMaxText = new Text('BET MAX', {
      fontFamily: 'Arial',
      fontSize: 22,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    this.betMaxText.anchor.set(0.5);
    this.betMaxText.x = buttonWidth / 2;
    this.betMaxText.y = buttonHeight / 2;
    this.betMaxButton.addChild(this.betMaxText);
    
    this.betMaxButton.on('pointertap', () => {
      // Set to maximum bet from bet levels
      const betLevels = getBetLevels();
      const maxBet = betLevels[betLevels.length - 1];
      this.updateBet(maxBet);
      // Apply bet and close
      this.applyBet();
      this.close();
    });
    
    this.betMaxButton.on('pointerover', () => {
      this.betMaxButton.scale.set(1.05);
    });
    this.betMaxButton.on('pointerout', () => {
      this.betMaxButton.scale.set(1.0);
    });
    
    this.contentContainer.addChild(this.betMaxButton);
  }

  private applyBet(): void {
    // Apply the selected bet
    this.callbacks.onBetSelected?.(this.selectedTotalBet);
  }

  public close(): void {
    // Only apply bet if popup is actually visible (user was interacting with it)
    // Don't apply bet when closing programmatically (e.g., when opening another popup)
    if (this.visible) {
      this.applyBet();
    }
    // Call onClose callback
    this.callbacks.onClose?.();
    // Call parent close (which handles visibility animation)
    super.close();
  }
  
  public closeWithoutApplying(): void {
    // Close without applying bet changes (for programmatic closes)
    this.callbacks.onClose?.();
    super.close();
  }

  public setBetButtonPosition(position: { x: number; y: number; width: number; height: number }): void {
    this.betButtonPosition = position;
    this.updatePosition();
  }

  private updatePosition(): void {
    if (!this.betButtonPosition) return;
    
    const betButtonCenterX = this.betButtonPosition.x + this.betButtonPosition.width / 2;
    const betButtonTop = this.betButtonPosition.y;
    
    // Position popup above the bet button, centered horizontally
    // Popup expands upward from the button
    const popupWidth = 500;
    const popupHeight = 480; // Match panel height
    const spacing = 10; // Small gap between button and popup
    
    // Center popup horizontally on bet button
    this.panel.x = betButtonCenterX - popupWidth / 2;
    // Position above bet button with small gap
    this.panel.y = betButtonTop - popupHeight - spacing;
    
    // Ensure popup doesn't go off-screen
    const padding = 20;
    if (this.panel.x < padding) {
      this.panel.x = padding;
    }
    if (this.panel.x + popupWidth > this.screenWidth - padding) {
      this.panel.x = this.screenWidth - popupWidth - padding;
    }
    if (this.panel.y < padding) {
      this.panel.y = betButtonTop + this.betButtonPosition.height + spacing; // Show below instead
    }
  }

  public updateLayout(width?: number, height?: number): void {
    if (width) this.screenWidth = width;
    if (height) this.screenHeight = height;
    // Adjust panel size for the new layout
    this.panel.width = 400;
    this.panel.height = 300;
    this.titleText.x = this.panel.width / 2;
    this.closeButton.x = this.panel.width - 50;
    this.updatePosition(); // Update position relative to bet button
    super.updateLayout(width || this.screenWidth, height || this.screenHeight);
  }
}
