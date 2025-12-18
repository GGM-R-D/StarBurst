import { Container, Graphics, Text } from 'pixi.js';
import gsap from 'gsap';
import { getNextBetAmount, getPreviousBetAmount, roundToTwoDecimals } from '@game/utils/betUtils';

export interface BottomBarCallbacks {
  onSpin?: () => void;
  onSpeedChange?: (speed: 1 | 2 | 3) => void;
  onBetDecrease?: () => void;
  onBetIncrease?: () => void;
  onBetClick?: () => void; // For clicking on bet panel to open bet popup
  onButtonClick?: () => void; // For click sound
  onAutoSpin?: () => void; // For auto spin button
  onBetChanged?: (lineBet: number, totalBet: number) => void; // Called when bet changes
}

/**
 * Bottom control bar matching Starburst EXACTLY.
 * Layout: BET PANEL (left) | SPIN BUTTON (center) | TURBO (right) | BALANCE (far right)
 */
export class BottomBar extends Container {
  private betPanel: Graphics;
  private betLabel: Text;
  private betValueText: Text;
  private betDecreaseButton: Graphics;
  private betIncreaseButton: Graphics;
  private autoSpinButton: Graphics;
  private autoSpinButtonText!: Text;
  private spinButton: Graphics;
  private spinButtonText!: Text;
  private turboButton: Graphics;
  private turboButtonText!: Text;
  private balancePanel: Graphics;
  private balanceLabel: Text;
  private balanceValueText: Text;
  private currentSpeed: 1 | 2 | 3 = 1;
  private readonly linesCount = 10; // Fixed 10 paylines
  private readonly availableLineBets: number[] = [0.10, 0.20, 0.50, 1.0, 2.0, 5.0, 10.0];
  private currentBetIndex: number = 4; // Default to 2.0 (index 4)
  private currentLineBet: number = 2.0; // Default line bet
  private currentBet: number = 20.0; // Default total bet (2.0 * 10)
  private currentBalance: number = 1000.0;
  private callbacks: BottomBarCallbacks;
  private screenWidth: number = 1920;
  private screenHeight: number = 1080;

  constructor(callbacks: BottomBarCallbacks = {}) {
    super();
    this.callbacks = callbacks;

    // Create bet panel
    this.betPanel = new Graphics();
    this.betLabel = new Text('BET', {
      fill: 0xffaa00, // Bright yellow-orange
      fontSize: 20,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    this.betValueText = new Text('2.50', {
      fill: 0xffaa00, // Bright yellow-orange
      fontSize: 18,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    // +/- buttons removed - bet panel is now a button that opens popup
    // this.betDecreaseButton = this.createBetControlButton('-');
    // this.betIncreaseButton = this.createBetControlButton('+');

    // Create auto-spin button (left of spin)
    this.autoSpinButton = this.createAutoSpinButton();

    // Create spin button (large green glowing circle - focal element)
    this.spinButton = this.createSpinButton();

    // Create turbo button
    this.turboButton = this.createTurboButton();

    // Create balance panel (matching bet panel style)
    this.balancePanel = new Graphics();
    this.balanceLabel = new Text('BALANCE', {
      fill: 0xffaa00, // Bright yellow-orange to match bet panel
      fontSize: 16,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    this.balanceValueText = new Text('1,000.00', {
      fill: 0xffaa00, // Bright yellow-orange to match bet panel
      fontSize: 32,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });

    // Add to container
    this.addChild(this.betPanel);
    this.addChild(this.betLabel);
    this.addChild(this.betValueText);
    // Remove +/- buttons - bet panel is now a button that opens popup
    // this.addChild(this.betDecreaseButton);
    // this.addChild(this.betIncreaseButton);
    this.addChild(this.autoSpinButton);
    this.addChild(this.spinButton);
    this.addChild(this.turboButton);
    this.addChild(this.balancePanel);
    this.addChild(this.balanceLabel);
    this.addChild(this.balanceValueText);

    // Setup interactions
    this.setupInteractions();

    // Update initial bet display (suppress callback during initialization)
    this.updateBetText(true);

    this.updateLayout();
  }

  /**
   * Create bet panel as a button with dark brown background and thin gold border.
   */
  private createBetPanel(width: number, height: number): void {
    this.betPanel.clear();

    // Dark brown background (pill-shaped) - styled as a button
    this.betPanel.beginFill(0x3d2817, 0.95); // Dark brown
    this.betPanel.lineStyle(2, 0xffd700, 1); // Thin gold border
    this.betPanel.drawRoundedRect(0, 0, width, height, height / 2);
    this.betPanel.endFill();
    
    // Make bet panel clickable to open bet popup
    this.betPanel.eventMode = 'static';
    this.betPanel.cursor = 'pointer';
    
    // Add hover effects to make it look like a button
    this.betPanel.on('pointerover', () => {
      this.betPanel.clear();
      this.betPanel.beginFill(0x4a3520, 1); // Lighter brown on hover
      this.betPanel.lineStyle(2, 0xffd700, 1);
      this.betPanel.drawRoundedRect(0, 0, width, height, height / 2);
      this.betPanel.endFill();
      this.betPanel.scale.set(1.05); // Slight scale on hover
    });
    
    this.betPanel.on('pointerout', () => {
      this.betPanel.clear();
      this.betPanel.beginFill(0x3d2817, 0.95); // Original color
      this.betPanel.lineStyle(2, 0xffd700, 1);
      this.betPanel.drawRoundedRect(0, 0, width, height, height / 2);
      this.betPanel.endFill();
      this.betPanel.scale.set(1.0); // Reset scale
    });
    
    this.betPanel.on('pointertap', () => {
      this.callbacks.onBetClick?.();
    });
  }

  /**
   * Create balance panel with dark brown background and thin gold border matching bet panel style.
   */
  private createBalancePanel(width: number, height: number): void {
    this.balancePanel.clear();

    // Dark brown background (pill-shaped)
    this.balancePanel.beginFill(0x3d2817, 0.95); // Dark brown
    this.balancePanel.lineStyle(2, 0xffd700, 1); // Thin gold border
    this.balancePanel.drawRoundedRect(0, 0, width, height, height / 2);
    this.balancePanel.endFill();
  }

  /**
   * Create bet control button (- or +) integrated into the pill shape.
   */
  private createBetControlButton(label: string): Graphics {
    const button = new Graphics();
    const radius = 16;

    // Gold border circle
    button.lineStyle(2, 0xffd700, 1);
    button.beginFill(0x3d2817, 0.95); // Dark brown to match panel
    button.drawCircle(0, 0, radius);
    button.endFill();

    // Label text (yellow-orange)
    const text = new Text(label, {
      fill: 0xffaa00, // Bright yellow-orange
      fontSize: 20,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    text.anchor.set(0.5);
    button.addChild(text);

    button.eventMode = 'static';
    button.cursor = 'pointer';

    button.on('pointerover', () => button.scale.set(1.1));
    button.on('pointerout', () => button.scale.set(1.0));

    return button;
  }


  /**
   * Create large green glossy spin button with thick gold glowing border matching image exactly.
   */
  private createSpinButton(): Graphics {
    const spin = new Graphics();
    const radius = 70;

    // Outer glow halo (bright yellow/gold glow effect - outside the button)
    spin.beginFill(0xffd700, 0.3);
    spin.drawCircle(0, 0, radius + 10);
    spin.endFill();
    spin.beginFill(0xffd700, 0.5);
    spin.drawCircle(0, 0, radius + 6);
    spin.endFill();

    // Main green button with thick gold border
    spin.lineStyle(5, 0xffd700, 1); // Thick gold border
    spin.beginFill(0x00cc00, 1); // Vibrant green fill
    spin.drawCircle(0, 0, radius);
    spin.endFill();

    // SPIN text (white, bold, with shadow/stroke)
    this.spinButtonText = new Text('SPIN', {
      fill: 0xffffff,
      fontSize: 34,
      fontFamily: 'Arial',
      fontWeight: 'bold',
      stroke: 0x000000,
      strokeThickness: 2,
      dropShadow: true,
      dropShadowDistance: 2,
      dropShadowBlur: 2
    });
    this.spinButtonText.anchor.set(0.5);
    this.spinButtonText.y = -6; // Slightly above center
    spin.addChild(this.spinButtonText);

    // White curved arrow at bottom edge, partially overlapping border
    const arrow = new Graphics();
    arrow.lineStyle(2.5, 0xffffff, 1);
    // Draw curved arrow pointing clockwise (right)
    const startX = -18;
    const startY = radius - 5; // Near bottom edge
    const endX = 18;
    const endY = radius - 5;
    const controlY = radius + 8; // Curve extends slightly past border
    arrow.moveTo(startX, startY);
    arrow.quadraticCurveTo(0, controlY, endX, endY);
    // Arrow head pointing right
    arrow.lineTo(endX - 4, endY - 2);
    arrow.moveTo(endX, endY);
    arrow.lineTo(endX - 2, endY + 2);
    spin.addChild(arrow);

    spin.eventMode = 'static';
    spin.cursor = 'pointer';

    // Hover effect
    spin.on('pointerover', () => {
      spin.scale.set(1.05);
    });
    spin.on('pointerout', () => {
      spin.scale.set(1.0);
    });

    return spin;
  }

  /**
   * Create auto-spin button with text label.
   */
  private createAutoSpinButton(): Graphics {
    const button = new Graphics();
    const radius = 28;

    // Gold border
    button.lineStyle(2, 0xffd700, 1);
    button.beginFill(0x3d2817, 0.95); // Dark brown
    button.drawCircle(0, 0, radius);
    button.endFill();

    // Text label
    this.autoSpinButtonText = new Text('AUTO', {
      fill: 0xffffff,
      fontSize: 12,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    this.autoSpinButtonText.anchor.set(0.5);
    button.addChild(this.autoSpinButtonText);

    button.eventMode = 'static';
    button.cursor = 'pointer';

    button.on('pointerover', () => button.scale.set(1.1));
    button.on('pointerout', () => button.scale.set(1.0));

    return button;
  }

  /**
   * Create turbo button with text label and speed indicator.
   */
  private createTurboButton(): Graphics {
    const button = new Graphics();
    const radius = 28;

    // Gold border
    button.lineStyle(2, 0xffd700, 1);
    button.beginFill(0x3d2817, 0.95); // Dark brown
    button.drawCircle(0, 0, radius);
    button.endFill();

    // Text label with speed indicator
    this.turboButtonText = new Text(`x${this.currentSpeed}`, {
      fill: 0xffffff,
      fontSize: 14,
      fontFamily: 'Arial',
      fontWeight: 'bold'
    });
    this.turboButtonText.anchor.set(0.5);
    button.addChild(this.turboButtonText);

    button.eventMode = 'static';
    button.cursor = 'pointer';

    button.on('pointerover', () => button.scale.set(1.1));
    button.on('pointerout', () => button.scale.set(1.0));

    return button;
  }

  private setupInteractions(): void {
    // Bet +/- buttons removed - bet panel is now a button that opens popup
    // Users adjust bet via the bet popup window

    this.spinButton.on('pointertap', () => {
      // Spin sound is handled in GameApp
      this.callbacks.onSpin?.();
    });

    this.turboButton.on('pointertap', () => {
      this.callbacks.onButtonClick?.();
      this.cycleSpeed();
    });

    this.autoSpinButton.on('pointertap', () => {
      this.callbacks.onButtonClick?.();
      this.callbacks.onAutoSpin?.();
    });
  }

  private cycleSpeed(): void {
    if (this.currentSpeed === 1) this.currentSpeed = 2;
    else if (this.currentSpeed === 2) this.currentSpeed = 3;
    else this.currentSpeed = 1;

    this.updateSpeedLabel();
    this.callbacks.onSpeedChange?.(this.currentSpeed);
  }

  private updateSpeedLabel(): void {
    // Update turbo button text with current speed
    if (this.turboButtonText) {
      this.turboButtonText.text = `x${this.currentSpeed}`;
    }
  }

  private updateBetText(suppressCallback: boolean = false): void {
    // Calculate total bet from line bet
    this.currentBet = this.currentLineBet * this.linesCount;
    // Display total bet (line bet * 10 lines)
    this.betValueText.text = this.currentBet.toFixed(2);
    
    // Notify callback of bet change (unless suppressed during initialization)
    if (!suppressCallback && this.callbacks.onBetChanged) {
      this.callbacks.onBetChanged(this.currentLineBet, this.currentBet);
    }
  }

  public setSpinEnabled(enabled: boolean): void {
    // Set button interactive state based on enabled flag
    this.spinButton.eventMode = enabled ? 'static' : 'none';
    this.spinButton.buttonMode = enabled;
    this.spinButton.cursor = enabled ? 'pointer' : 'default';
    this.spinButton.alpha = enabled ? 1 : 0.6; // More visibly disabled when not enabled
    console.info(`[BottomBar] Spin button enabled: ${enabled}, interactive: ${this.spinButton.interactive}`);
  }

  public setSpinButtonText(text: string): void {
    if (this.spinButtonText) {
      this.spinButtonText.text = text;
    }
  }

  public getCurrentBet(): number {
    // Round to 2 decimal places to avoid floating-point precision issues
    return roundToTwoDecimals(this.currentBet);
  }

  public setSpeed(speed: 1 | 2 | 3): void {
    this.currentSpeed = speed;
    this.updateSpeedLabel();
  }

  public getCurrentSpeed(): 1 | 2 | 3 {
    return this.currentSpeed;
  }

  public setBalance(value: number): void {
    this.currentBalance = value;
    this.balanceValueText.text = value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  public setWin(value: number): void {
    // WIN display removed - method kept for compatibility
  }

  public setBet(value: number): void {
    // This sets total bet - convert to line bet
    const lineBet = roundToTwoDecimals(value / this.linesCount);
    // Only update if the bet actually changed to avoid unnecessary callbacks
    if (Math.abs(this.currentLineBet - lineBet) > 0.001) {
      this.setLineBet(lineBet);
    }
  }
  
  public getCurrentLineBet(): number {
    return roundToTwoDecimals(this.currentLineBet);
  }
  
  public getCurrentTotalBet(): number {
    return roundToTwoDecimals(this.currentBet);
  }
  
  public setLineBet(lineBet: number): void {
    // Only update if bet actually changed to avoid unnecessary callbacks
    if (Math.abs(this.currentLineBet - lineBet) < 0.001) {
      return; // Bet hasn't changed, skip update
    }
    
    // Find closest available bet
    let closestIndex = 0;
    let closestDiff = Math.abs(this.availableLineBets[0] - lineBet);
    for (let i = 1; i < this.availableLineBets.length; i++) {
      const diff = Math.abs(this.availableLineBets[i] - lineBet);
      if (diff < closestDiff) {
        closestDiff = diff;
        closestIndex = i;
      }
    }
    this.currentBetIndex = closestIndex;
    this.currentLineBet = this.availableLineBets[this.currentBetIndex];
    this.updateBetText();
  }
  
  public setBetEnabled(enabled: boolean): void {
    // Bet panel is now a button that opens popup, so we don't disable it
    // But we could disable the bet popup interaction if needed
    // For now, bet changes are handled via the bet popup
  }

  public getBetButtonPosition(): { x: number; y: number; width: number; height: number } {
    const bottomBarY = this.screenHeight - 120;
    const centerX = this.screenWidth / 2;
    const betPanelWidth = 180;
    const betPanelHeight = 70;
    const betPanelX = centerX - 400;
    
    return {
      x: betPanelX,
      y: bottomBarY,
      width: betPanelWidth,
      height: betPanelHeight
    };
  }

  public updateLayout(width?: number, height?: number): void {
    if (width) this.screenWidth = width;
    if (height) this.screenHeight = height;

    const bottomBarY = this.screenHeight - 120; // Starburst bottom bar position
    const centerX = this.screenWidth / 2;
    const spacing = 25; // Spacing between elements

    // Bet panel (moved closer to spin button) - matching Starburst proportions
    const betPanelWidth = 180;
    const betPanelHeight = 70;
    this.createBetPanel(betPanelWidth, betPanelHeight);
    const betPanelX = centerX - 400; // Moved to the left
    this.betPanel.x = betPanelX;
    this.betPanel.y = bottomBarY;

    // Bet label (centered, top portion of button)
    this.betLabel.anchor.set(0.5, 0.5);
    this.betLabel.x = betPanelX + betPanelWidth / 2;
    this.betLabel.y = bottomBarY + 25; // Top portion

    // Bet value (centered below label)
    this.betValueText.anchor.set(0.5, 0.5);
    this.betValueText.x = betPanelX + betPanelWidth / 2;
    this.betValueText.y = bottomBarY + 50; // Bottom portion

    // +/- buttons removed - bet panel is now a button that opens popup

    // Auto-spin button (left of spin, spaced out)
    this.autoSpinButton.x = centerX - 120;
    this.autoSpinButton.y = bottomBarY + 35;

    // Spin button (center) - focal element
    this.spinButton.x = centerX;
    this.spinButton.y = bottomBarY + 35;

    // Turbo button (right of spin, spaced out)
    this.turboButton.x = centerX + 120;
    this.turboButton.y = bottomBarY + 35;

    // Balance panel (right of spin button, matching bet panel style)
    const balancePanelWidth = 200;
    const balancePanelHeight = 70;
    this.createBalancePanel(balancePanelWidth, balancePanelHeight);
    const balancePanelX = centerX + 200; // To the right of spin button
    this.balancePanel.x = balancePanelX;
    this.balancePanel.y = bottomBarY;

    // Balance label (centered above value, proper spacing)
    this.balanceLabel.anchor.set(0.5, 0.5);
    this.balanceLabel.x = balancePanelX + balancePanelWidth / 2;
    this.balanceLabel.y = bottomBarY + 22; // Higher up, with space below

    // Balance value (centered below label, with proper gap)
    this.balanceValueText.anchor.set(0.5, 0.5);
    this.balanceValueText.x = balancePanelX + balancePanelWidth / 2;
    this.balanceValueText.y = bottomBarY + 42; // Lower, with gap from label
  }
}
