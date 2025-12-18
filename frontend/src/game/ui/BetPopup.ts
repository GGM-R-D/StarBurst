import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import { Modal } from './Modal';
import { roundToTwoDecimals, getNextBetAmount, getPreviousBetAmount } from '@game/utils/betUtils';

export interface BetPopupCallbacks {
  onBetSelected?: (bet: number) => void;
  onClose?: () => void;
}

const NUM_LINES = 10; // Starburst has 10 fixed paylines

/**
 * Bet selection popup matching the reference design.
 * Shows COINS PER LINE, COIN VALUE, and TOTAL BET with +/- controls.
 */
export class BetPopup extends Modal {
  private callbacks: BetPopupCallbacks;
  private selectedTotalBet: number = 2.50;
  
  // Coin system values
  private coinsPerLine: number = 5;
  private coinValue: number = 0.05;
  
  // UI elements
  private coinsPerLineValueText: Text;
  private coinValueText: Text;
  private totalBetText: Text;
  private betMaxButton: Graphics;
  private betMaxText: Text;
  
  // Buttons
  private coinsPerLineMinusButton: Graphics;
  private coinsPerLinePlusButton: Graphics;
  private coinValueMinusButton: Graphics;
  private coinValuePlusButton: Graphics;
  private totalBetMinusButton: Graphics;
  private totalBetPlusButton: Graphics;
  
  // Bet button position for relative positioning
  private betButtonPosition: { x: number; y: number; width: number; height: number } | null = null;

  constructor(callbacks: BetPopupCallbacks = {}, currentBet: number = 2.50) {
    super('BETTING ON 10 LINES', false);
    this.callbacks = callbacks;
    this.selectedTotalBet = roundToTwoDecimals(currentBet);
    
    // Convert total bet to coin system
    this.updateCoinSystemFromTotalBet(this.selectedTotalBet);
    
    this.setupUI();
  }

  private updateCoinSystemFromTotalBet(totalBet: number): void {
    // Try to find reasonable coin values
    // For 2.50: 5 coins * 0.05 = 0.25 per line * 10 = 2.50
    // Try common coin values: 0.01, 0.02, 0.05, 0.10, 0.20, 0.25, 0.50, 1.00
    const betPerLine = totalBet / NUM_LINES;
    const commonCoinValues = [0.01, 0.02, 0.05, 0.10, 0.20, 0.25, 0.50, 1.00];
    
    // Find the best coin value that gives whole number of coins
    let bestCoinValue = 0.05;
    let bestCoinsPerLine = Math.round(betPerLine / bestCoinValue);
    
    for (const coinVal of commonCoinValues) {
      const coins = betPerLine / coinVal;
      if (coins >= 1 && Number.isInteger(coins) && coins <= 100) {
        bestCoinValue = coinVal;
        bestCoinsPerLine = coins;
        break;
      }
    }
    
    // If no perfect match, use closest reasonable values
    if (bestCoinsPerLine === 0 || bestCoinsPerLine > 100) {
      bestCoinValue = 0.05;
      bestCoinsPerLine = Math.max(1, Math.round(betPerLine / bestCoinValue));
    }
    
    this.coinValue = roundToTwoDecimals(bestCoinValue);
    this.coinsPerLine = bestCoinsPerLine;
  }

  private calculateTotalBet(): number {
    return roundToTwoDecimals(this.coinsPerLine * this.coinValue * NUM_LINES);
  }

  private setupUI(): void {
    const panelWidth = 500;
    const panelHeight = 480; // Increased to ensure everything fits
    
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
    
    // Section spacing - reduced to fit better
    const sectionSpacing = 75;
    const startY = 80; // Moved up slightly
    
    // COINS PER LINE section
    this.createSection(
      'COINS PER LINE',
      this.coinsPerLine.toString(),
      startY,
      (value: number) => {
        this.coinsPerLine = Math.max(1, Math.min(100, value));
        this.updateTotalBet();
      },
      () => {
        this.coinsPerLine = Math.max(1, this.coinsPerLine - 1);
        this.updateTotalBet();
      },
      () => {
        this.coinsPerLine = Math.min(100, this.coinsPerLine + 1);
        this.updateTotalBet();
      }
    );
    
    // COIN VALUE section
    this.createSection(
      'COIN VALUE',
      `€${this.coinValue.toFixed(2)}`,
      startY + sectionSpacing,
      (value: number) => {
        this.coinValue = roundToTwoDecimals(Math.max(0.01, Math.min(10.00, value)));
        this.updateTotalBet();
      },
      () => {
        const newValue = roundToTwoDecimals(Math.max(0.01, this.coinValue - 0.01));
        this.coinValue = newValue;
        this.updateTotalBet();
      },
      () => {
        const newValue = roundToTwoDecimals(Math.min(10.00, this.coinValue + 0.01));
        this.coinValue = newValue;
        this.updateTotalBet();
      }
    );
    
    // TOTAL BET section
    this.createSection(
      'TOTAL BET',
      `€${this.calculateTotalBet().toFixed(2)}`,
      startY + sectionSpacing * 2,
      undefined, // Read-only or adjust via total bet
      () => {
        const currentTotal = this.calculateTotalBet();
        const newTotal = getPreviousBetAmount(currentTotal);
        this.updateFromTotalBet(newTotal);
      },
      () => {
        const currentTotal = this.calculateTotalBet();
        const newTotal = getNextBetAmount(currentTotal);
        this.updateFromTotalBet(newTotal);
      }
    );
    
    // BET MAX button - positioned after all sections
    this.createBetMaxButton(startY + sectionSpacing * 3 + 10);
  }

  private createSection(
    label: string,
    value: string,
    y: number,
    onValueChange?: (value: number) => void,
    onMinus?: () => void,
    onPlus?: () => void
  ): void {
    const labelText = new Text(label, {
      fontFamily: 'Arial',
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    labelText.anchor.set(0.5, 0);
    labelText.x = 250; // Center of 500px panel
    labelText.y = y;
    this.contentContainer.addChild(labelText);
    
    // Value field (dark grey rectangle)
    const fieldWidth = 180; // Standard size
    const fieldHeight = 40;
    const buttonRadius = 25;
    const buttonSpacing = 35; // Space between button edge and field edge
    
    // Calculate total width: button diameter + spacing + field + spacing + button diameter
    // Button diameter = radius * 2 = 50
    const buttonDiameter = buttonRadius * 2;
    const totalGroupWidth = buttonDiameter + buttonSpacing + fieldWidth + buttonSpacing + buttonDiameter;
    const groupLeftEdge = (500 - totalGroupWidth) / 2; // Center the entire group
    
    // Position elements relative to the centered group
    // Button centers are at: leftEdge + radius, and rightEdge - radius
    const minusButtonX = groupLeftEdge + buttonRadius;
    const fieldX = groupLeftEdge + buttonDiameter + buttonSpacing;
    const plusButtonX = groupLeftEdge + buttonDiameter + buttonSpacing + fieldWidth + buttonSpacing + buttonRadius;
    const fieldY = y + 30;
    
    const valueField = new Graphics();
    valueField.beginFill(0x2a2a2a, 1);
    valueField.lineStyle(1, 0x444444, 1);
    valueField.drawRoundedRect(0, 0, fieldWidth, fieldHeight, 5);
    valueField.endFill();
    valueField.x = fieldX;
    valueField.y = fieldY;
    this.contentContainer.addChild(valueField);
    
    // Value text
    const valueText = new Text(value, {
      fontFamily: 'Arial',
      fontSize: 24,
      fontWeight: 'bold',
      fill: '#ffffff',
      align: 'center'
    });
    valueText.anchor.set(0.5);
    valueText.x = fieldX + fieldWidth / 2;
    valueText.y = fieldY + fieldHeight / 2;
    this.contentContainer.addChild(valueText);
    
    // Store reference for updates
    if (label === 'COINS PER LINE') {
      this.coinsPerLineValueText = valueText;
    } else if (label === 'COIN VALUE') {
      this.coinValueText = valueText;
    } else if (label === 'TOTAL BET') {
      this.totalBetText = valueText;
    }
    
    // Minus button (white circle with black minus)
    const minusButton = this.createControlButton('-', minusButtonX, fieldY + fieldHeight / 2, false);
    minusButton.on('pointertap', () => {
      onMinus?.();
    });
    this.contentContainer.addChild(minusButton);
    
    if (label === 'COINS PER LINE') {
      this.coinsPerLineMinusButton = minusButton;
    } else if (label === 'COIN VALUE') {
      this.coinValueMinusButton = minusButton;
    } else if (label === 'TOTAL BET') {
      this.totalBetMinusButton = minusButton;
    }
    
    // Plus button (green circle with white plus)
    const plusButton = this.createControlButton('+', plusButtonX, fieldY + fieldHeight / 2, true);
    plusButton.on('pointertap', () => {
      onPlus?.();
    });
    this.contentContainer.addChild(plusButton);
    
    if (label === 'COINS PER LINE') {
      this.coinsPerLinePlusButton = plusButton;
    } else if (label === 'COIN VALUE') {
      this.coinValuePlusButton = plusButton;
    } else if (label === 'TOTAL BET') {
      this.totalBetPlusButton = plusButton;
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
    const buttonWidth = 200; // Standard size
    const buttonHeight = 40; // Slightly smaller
    const buttonX = (500 - buttonWidth) / 2; // Adjusted for new panel width
    
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
      // Set to maximum bet (100)
      this.updateFromTotalBet(100);
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

  private updateFromTotalBet(totalBet: number): void {
    this.selectedTotalBet = roundToTwoDecimals(totalBet);
    this.updateCoinSystemFromTotalBet(this.selectedTotalBet);
    this.updateTotalBet();
  }

  private updateTotalBet(): void {
    const totalBet = this.calculateTotalBet();
    this.selectedTotalBet = totalBet;
    
    // Update display texts
    if (this.coinsPerLineValueText) {
      this.coinsPerLineValueText.text = this.coinsPerLine.toString();
    }
    if (this.coinValueText) {
      this.coinValueText.text = `€${this.coinValue.toFixed(2)}`;
    }
    if (this.totalBetText) {
      this.totalBetText.text = `€${totalBet.toFixed(2)}`;
    }
  }

  private applyBet(): void {
    // Apply the selected bet
    const totalBet = this.calculateTotalBet();
    this.selectedTotalBet = roundToTwoDecimals(totalBet);
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
    this.panel.width = 500;
    this.panel.height = 480; // Increased to ensure everything fits
    this.titleText.x = this.panel.width / 2;
    this.closeButton.x = this.panel.width - 50;
    this.updatePosition(); // Update position relative to bet button
    super.updateLayout(width || this.screenWidth, height || this.screenHeight);
  }
}
