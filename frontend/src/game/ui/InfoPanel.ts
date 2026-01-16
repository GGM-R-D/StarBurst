// src/game/ui/InfoPanel.ts
import { Container, Graphics, Text, Sprite, Texture } from 'pixi.js';
import { SYMBOL_PAYTABLE, RULES_TEXT, PAYLINES_CONFIG } from '../config/paytableConfig';
import type { SymbolId } from '@game/demo/SpinTypes';
import { createSymbolSprite } from '@game/symbols/SymbolFactory';

type InfoTab = 'paytable' | 'rules';

export interface InfoPanelCallbacks {
  onClose?: () => void;
}

export class InfoPanel extends Container {
  private overlay: Graphics;
  private panelBg: Graphics;
  private titleText: Text;
  private paytableContainer: Container;
  private rulesContainer: Container;
  private paytableTabButton: Container;
  private rulesTabButton: Container;
  private closeButton: Container;
  private callbacks: InfoPanelCallbacks;
  private viewportWidth: number;
  private viewportHeight: number;
  private currentLineBet: number = 0.10; // Default line bet (R1 total bet / 10 lines)
  private _contentX: number = 0;
  private _contentY: number = 0;
  private _contentWidth: number = 0;
  private _contentHeight: number = 0;
  private paytableMask: Graphics;
  private rulesMask: Graphics;

  constructor(viewportWidth: number, viewportHeight: number, initialLineBet: number = 0.10, callbacks: InfoPanelCallbacks = {}) {
    super();
    this.viewportWidth = viewportWidth;
    this.viewportHeight = viewportHeight;
    this.callbacks = callbacks;
    this.currentLineBet = initialLineBet;

    // Dim background overlay
    this.overlay = new Graphics();
    this.overlay.beginFill(0x000000, 0.7);
    this.overlay.drawRect(0, 0, viewportWidth, viewportHeight);
    this.overlay.endFill();
    this.overlay.eventMode = 'static';
    this.overlay.cursor = 'default';
    // Close panel when clicking overlay (but not the panel itself)
    this.overlay.on('pointertap', (e) => {
      // Only close if clicking directly on overlay, not if event bubbled from panel
      if (e.target === this.overlay) {
        this.hide();
        this.callbacks.onClose?.(); // Call callback when closing via overlay
      }
    });
    this.addChild(this.overlay);

    const panelWidth = viewportWidth * 0.8; // Increased from 0.6 to 0.8 (80% width)
    const panelHeight = viewportHeight * 0.85; // Increased from 0.7 to 0.85 (85% height)

    // Main panel background
    this.panelBg = new Graphics();
    this.panelBg.beginFill(0x1b0a2f, 0.95);
    this.panelBg.lineStyle(4, 0x9b59ff, 1);
    this.panelBg.drawRoundedRect(0, 0, panelWidth, panelHeight, 24);
    this.panelBg.endFill();
    this.panelBg.x = (viewportWidth - panelWidth) / 2;
    this.panelBg.y = (viewportHeight - panelHeight) / 2;
    this.addChild(this.panelBg);

    const panelCenterX = this.panelBg.x + panelWidth / 2;

    // Title
    this.titleText = new Text('PAYTABLE', {
      fontFamily: 'Arial',
      fontSize: 40,
      fill: 0xffffff,
      align: 'center',
      fontWeight: 'bold'
    });
    this.titleText.anchor.set(0.5, 0);
    this.titleText.x = panelCenterX;
    this.titleText.y = this.panelBg.y + 16;
    this.addChild(this.titleText);

    // Tabs
    this.paytableTabButton = this.createTabButton('PAYTABLE');
    this.paytableTabButton.x = this.panelBg.x + 40;
    this.paytableTabButton.y = this.titleText.y + this.titleText.height + 10;
    this.addChild(this.paytableTabButton);

    this.rulesTabButton = this.createTabButton('RULES');
    this.rulesTabButton.x = this.paytableTabButton.x + 160;
    this.rulesTabButton.y = this.paytableTabButton.y;
    this.addChild(this.rulesTabButton);

    this.paytableTabButton.eventMode = 'static';
    this.paytableTabButton.cursor = 'pointer';
    this.paytableTabButton.on('pointertap', () => this.setTab('paytable'));

    this.rulesTabButton.eventMode = 'static';
    this.rulesTabButton.cursor = 'pointer';
    this.rulesTabButton.on('pointertap', () => this.setTab('rules'));

    // Close button (top-right of the panel)
    this.closeButton = this.createCloseButton();
    this.closeButton.x = this.panelBg.x + panelWidth - 22;
    this.closeButton.y = this.panelBg.y + 22;
    this.addChild(this.closeButton);

    // Containers for tab content (will be scrollable)
    this.paytableContainer = new Container();
    this.rulesContainer = new Container();
    this.addChild(this.paytableContainer);
    this.addChild(this.rulesContainer);
    
    // Create masks for scrollable content
    this.paytableMask = new Graphics();
    this.rulesMask = new Graphics();
    this.paytableContainer.mask = this.paytableMask;
    this.rulesContainer.mask = this.rulesMask;
    this.addChild(this.paytableMask);
    this.addChild(this.rulesMask);

    const contentX = this.panelBg.x + 40;
    const contentY = this.paytableTabButton.y + 50;
    const contentWidth = panelWidth - 80;
    const contentHeight = panelHeight - (contentY - this.panelBg.y) - 40;

    // Store content layout for re-use
    this._contentX = contentX;
    this._contentY = contentY;
    this._contentWidth = contentWidth;
    this._contentHeight = contentHeight;

    // Setup scrollable containers with masks
    this.setupScrollableContainers(contentX, contentY, contentWidth, contentHeight);

    this.buildPaytable(contentX, contentY, contentWidth, contentHeight);
    this.buildRules(contentX, contentY, contentWidth, contentHeight);

    this.visible = false;
    this.eventMode = 'static';

    this.setTab('paytable');
  }

  public show(initialTab: InfoTab = 'paytable'): void {
    this.visible = true;
    this.setTab(initialTab);
  }

  public hide(): void {
    if (!this.visible) {
      return; // Already hidden, prevent infinite loop
    }
    this.visible = false;
    // Don't call onClose here - it's called by the close button directly
    // This prevents infinite loop when closeInfoPanel calls hide()
  }
  
  private setupScrollableContainers(x: number, y: number, width: number, height: number): void {
    // Create/update masks for scrollable content
    this.paytableMask.clear();
    this.paytableMask.beginFill(0xffffff, 1);
    this.paytableMask.drawRect(x, y, width, height);
    this.paytableMask.endFill();
    this.paytableMask.x = 0;
    this.paytableMask.y = 0;

    this.rulesMask.clear();
    this.rulesMask.beginFill(0xffffff, 1);
    this.rulesMask.drawRect(x, y, width, height);
    this.rulesMask.endFill();
    this.rulesMask.x = 0;
    this.rulesMask.y = 0;
  }

  public updateForBet(lineBet: number): void {
    this.currentLineBet = lineBet;
    // Rebuild the paytable content with new amounts
    this.buildPaytable(this._contentX, this._contentY, this._contentWidth, this._contentHeight);
  }

  public updateLayout(width: number, height: number): void {
    this.viewportWidth = width;
    this.viewportHeight = height;

    // Update overlay
    this.overlay.clear();
    this.overlay.beginFill(0x000000, 0.7);
    this.overlay.drawRect(0, 0, width, height);
    this.overlay.endFill();

    const panelWidth = width * 0.8; // Increased from 0.6 to 0.8 (80% width)
    const panelHeight = height * 0.85; // Increased from 0.7 to 0.85 (85% height)

    // Update panel position
    this.panelBg.clear();
    this.panelBg.beginFill(0x1b0a2f, 0.95);
    this.panelBg.lineStyle(4, 0x9b59ff, 1);
    this.panelBg.drawRoundedRect(0, 0, panelWidth, panelHeight, 24);
    this.panelBg.endFill();
    this.panelBg.x = (width - panelWidth) / 2;
    this.panelBg.y = (height - panelHeight) / 2;

    // Update title position
    const panelCenterX = this.panelBg.x + panelWidth / 2;
    this.titleText.x = panelCenterX;
    this.titleText.y = this.panelBg.y + 16;

    // Update tab positions
    this.paytableTabButton.x = this.panelBg.x + 40;
    this.paytableTabButton.y = this.titleText.y + this.titleText.height + 10;
    this.rulesTabButton.x = this.paytableTabButton.x + 160;
    this.rulesTabButton.y = this.paytableTabButton.y;

    // Update close button
    this.closeButton.x = this.panelBg.x + panelWidth - 22;
    this.closeButton.y = this.panelBg.y + 22;

    // Rebuild content with new dimensions
    const contentX = this.panelBg.x + 40;
    const contentY = this.paytableTabButton.y + 50;
    const contentWidth = panelWidth - 80;
    const contentHeight = panelHeight - (contentY - this.panelBg.y) - 40;

    // Store content layout for re-use
    this._contentX = contentX;
    this._contentY = contentY;
    this._contentWidth = contentWidth;
    this._contentHeight = contentHeight;

    // Update scrollable containers
    this.setupScrollableContainers(contentX, contentY, contentWidth, contentHeight);

    this.buildPaytable(contentX, contentY, contentWidth, contentHeight);
    this.buildRules(contentX, contentY, contentWidth, contentHeight);
  }

  private createTabButton(label: string): Container {
    const container = new Container();
    const bg = new Graphics();
    bg.beginFill(0x3b1b73, 1);
    bg.lineStyle(2, 0x9b59ff, 1);
    bg.drawRoundedRect(0, 0, 140, 32, 16);
    bg.endFill();
    container.addChild(bg);

    const text = new Text(label, {
      fontFamily: 'Arial',
      fontSize: 16,
      fill: 0xffffff,
      fontWeight: 'bold'
    });
    text.anchor.set(0.5);
    text.x = bg.width / 2;
    text.y = bg.height / 2;
    container.addChild(text);

    (container as any)._bg = bg;
    (container as any)._label = text;

    return container;
  }

  private createCloseButton(): Container {
    const container = new Container();
    const bg = new Graphics();
    bg.beginFill(0x9b59ff, 1);
    bg.drawCircle(0, 0, 12);
    bg.endFill();
    container.addChild(bg);

    const text = new Text('X', {
      fontFamily: 'Arial',
      fontSize: 14,
      fill: 0xffffff,
      fontWeight: 'bold'
    });
    text.anchor.set(0.5);
    text.x = 0;
    text.y = 0;
    container.addChild(text);

    container.eventMode = 'static';
    container.cursor = 'pointer';
    container.on('pointertap', () => {
      this.hide();
      this.callbacks.onClose?.(); // Call callback directly from close button
    });

    return container;
  }

  private setTab(tab: InfoTab): void {
    // Visual toggle tabs
    this.updateTabVisual(this.paytableTabButton, tab === 'paytable');
    this.updateTabVisual(this.rulesTabButton, tab === 'rules');

    this.paytableContainer.visible = tab === 'paytable';
    this.rulesContainer.visible = tab === 'rules';

    this.titleText.text = tab === 'paytable' ? 'PAYTABLE' : 'RULES';
  }

  private updateTabVisual(tabContainer: Container, active: boolean): void {
    const bg = (tabContainer as any)._bg as Graphics;
    const label = (tabContainer as any)._label as Text;
    bg.tint = active ? 0xffffff : 0xffffff;
    bg.alpha = active ? 1.0 : 0.6;
    label.style.fill = active ? 0x000000 : 0xffffff;
  }

  private buildPaytable(x: number, y: number, width: number, height: number): void {
    this.paytableContainer.removeChildren();
    this.paytableContainer.x = 0;
    this.paytableContainer.y = 0;

    let currentY = y;
    const sectionSpacing = 50; // Increased spacing between sections

    // Section 1: PAYLINES (5x3 matrix for each payline)
    const paylinesTitle = new Text('PAYLINES', {
      fontFamily: 'Arial',
      fontSize: 22,
      fill: 0xffd700,
      fontWeight: 'bold'
    });
    paylinesTitle.x = x;
    paylinesTitle.y = currentY;
    this.paytableContainer.addChild(paylinesTitle);
    currentY += paylinesTitle.height + 25;

    // Create payline matrices - better organized layout
    const matrixSize = 18; // Slightly larger for better visibility
    const matrixSpacing = 4; // More spacing between cells
    const paylineVerticalSpacing = 25; // Spacing between payline rows
    const paylineHorizontalSpacing = 60; // Spacing between payline columns (reduced for 5 per row)
    const paylinesPerRow = 5; // Show 5 paylines per row
    let paylineX = x;
    let paylineRowStartY = currentY;
    let maxRowHeight = 0;

    for (let i = 0; i < PAYLINES_CONFIG.length; i++) {
      const payline = PAYLINES_CONFIG[i];
      const isNewRow = i % paylinesPerRow === 0;
      
      if (isNewRow && i > 0) {
        // Start new row - move down by the max height of previous row
        paylineX = x;
        currentY += maxRowHeight + paylineVerticalSpacing;
        paylineRowStartY = currentY;
        maxRowHeight = 0;
      } else if (!isNewRow) {
        // Move to next column in same row
        paylineX += 5 * (matrixSize + matrixSpacing) + paylineHorizontalSpacing;
      }
      
      // Create payline label
      const paylineLabel = new Text(`Payline ${payline.id}`, {
        fontFamily: 'Arial',
        fontSize: 12,
        fill: 0xffffff,
        fontWeight: 'bold'
      });
      paylineLabel.x = paylineX;
      paylineLabel.y = paylineRowStartY;
      this.paytableContainer.addChild(paylineLabel);
      
      const matrixStartY = paylineRowStartY + paylineLabel.height + 8;

      // Create 5x3 matrix (5 reels, 3 rows)
      for (let reel = 0; reel < 5; reel++) {
        for (let row = 0; row < 3; row++) {
          const cellX = paylineX + reel * (matrixSize + matrixSpacing);
          const cellY = matrixStartY + row * (matrixSize + matrixSpacing);
          
          // Check if this position is part of the payline
          const isActive = payline.rows[reel] === row;
          
          const cell = new Graphics();
          cell.beginFill(isActive ? 0x9b59ff : 0x333333, 1);
          cell.lineStyle(1.5, isActive ? 0xffffff : 0x666666, 1);
          cell.drawRect(0, 0, matrixSize, matrixSize);
          cell.endFill();
          cell.x = cellX;
          cell.y = cellY;
          this.paytableContainer.addChild(cell);
        }
      }

      // Calculate height of this payline (label + matrix)
      const matrixHeight = 3 * (matrixSize + matrixSpacing);
      const totalHeight = paylineLabel.height + 8 + matrixHeight;
      maxRowHeight = Math.max(maxRowHeight, totalHeight);
    }

    // Update currentY to after last row
    currentY += maxRowHeight;
    currentY += sectionSpacing;

    // Section 2: PAYOUTS (Table format with dynamic amounts)
    const payoutsTitle = new Text('PAYOUTS (Bet per line: ' + this.currentLineBet.toFixed(2) + ')', {
      fontFamily: 'Arial',
      fontSize: 22,
      fill: 0xffd700,
      fontWeight: 'bold'
    });
    payoutsTitle.x = x;
    payoutsTitle.y = currentY;
    this.paytableContainer.addChild(payoutsTitle);
    currentY += payoutsTitle.height + 25;

    // Table headers with better spacing
    const headerY = currentY;
    const headerStyle = {
      fontFamily: 'Arial',
      fontSize: 16,
      fill: 0xffd700,
      fontWeight: 'bold'
    };

    const symbolHeader = new Text('Symbol', headerStyle);
    symbolHeader.x = x;
    symbolHeader.y = headerY;
    this.paytableContainer.addChild(symbolHeader);

    const x3Header = new Text('3x', headerStyle);
    x3Header.x = x + 180;
    x3Header.y = headerY;
    this.paytableContainer.addChild(x3Header);

    const x4Header = new Text('4x', headerStyle);
    x4Header.x = x + 260;
    x4Header.y = headerY;
    this.paytableContainer.addChild(x4Header);

    const x5Header = new Text('5x', headerStyle);
    x5Header.x = x + 340;
    x5Header.y = headerY;
    this.paytableContainer.addChild(x5Header);

    // Draw header underline
    const headerLine = new Graphics();
    headerLine.lineStyle(2, 0xffd700, 0.5);
    headerLine.moveTo(x, headerY + 25);
    headerLine.lineTo(x + 420, headerY + 25);
    this.paytableContainer.addChild(headerLine);

    currentY += 35;

    // Table rows for each symbol with better spacing
    const rowSpacing = 45;
    const iconSize = 36;
    
    // Calculate right side position for wild section
    const rightSideX = x + 450; // Position to the right of the payouts table
    let rightSideY = currentY; // Start at same Y as payouts table

    for (const entry of SYMBOL_PAYTABLE) {
      if (entry.isWild) {
        continue; // Wild handled separately on the right side
      }

      // Icon
      try {
        const sprite = createSymbolSprite(entry.id);
        sprite.width = iconSize;
        sprite.height = iconSize;
        sprite.x = x;
        sprite.y = currentY - 2;
        sprite.anchor.set(0, 0);
        this.paytableContainer.addChild(sprite);
      } catch (error) {
        console.warn(`Failed to create sprite for ${entry.id}:`, error);
      }

      // Symbol name
      const nameText = new Text(entry.displayName, {
        fontFamily: 'Arial',
        fontSize: 15,
        fill: 0xffffff,
        fontWeight: '500'
      });
      nameText.x = x + iconSize + 12;
      nameText.y = currentY + 6;
      this.paytableContainer.addChild(nameText);

      // Payout amounts (calculated based on current line bet)
      const pays = entry.pays;
      const lb = this.currentLineBet;
      const p3 = pays[3] != null ? (pays[3] * lb).toFixed(2) : '-';
      const p4 = pays[4] != null ? (pays[4] * lb).toFixed(2) : '-';
      const p5 = pays[5] != null ? (pays[5] * lb).toFixed(2) : '-';

      const payoutStyle = {
        fontFamily: 'Arial',
        fontSize: 15,
        fill: 0xffffff,
        fontWeight: '500'
      };

      const p3Text = new Text(p3, payoutStyle);
      p3Text.x = x + 180;
      p3Text.y = currentY + 6;
      this.paytableContainer.addChild(p3Text);

      const p4Text = new Text(p4, payoutStyle);
      p4Text.x = x + 260;
      p4Text.y = currentY + 6;
      this.paytableContainer.addChild(p4Text);

      const p5Text = new Text(p5, payoutStyle);
      p5Text.x = x + 340;
      p5Text.y = currentY + 6;
      this.paytableContainer.addChild(p5Text);

      currentY += rowSpacing;
    }

    // Wild section on the right side of the panel
    const wildEntry = SYMBOL_PAYTABLE.find(e => e.isWild);
    if (wildEntry) {
      // Draw vertical separator line
      const separatorLine = new Graphics();
      separatorLine.lineStyle(1, 0x666666, 0.5);
      separatorLine.moveTo(rightSideX - 20, rightSideY - 10);
      separatorLine.lineTo(rightSideX - 20, rightSideY + 300);
      this.paytableContainer.addChild(separatorLine);

      try {
        const sprite = createSymbolSprite(wildEntry.id);
        sprite.width = iconSize;
        sprite.height = iconSize;
        sprite.x = rightSideX;
        sprite.y = rightSideY;
        sprite.anchor.set(0, 0);
        this.paytableContainer.addChild(sprite);
      } catch (error) {
        console.warn(`Failed to create sprite for ${wildEntry.id}:`, error);
      }

      const wildTitle = new Text('WILD SYMBOL', {
        fontFamily: 'Arial',
        fontSize: 16,
        fill: 0xffe66d,
        fontWeight: 'bold'
      });
      wildTitle.x = rightSideX + iconSize + 12;
      wildTitle.y = rightSideY + 6;
      this.paytableContainer.addChild(wildTitle);

      const wildDesc = new Text(wildEntry.description ?? '', {
        fontFamily: 'Arial',
        fontSize: 13,
        fill: 0xcccccc,
        wordWrap: true,
        wordWrapWidth: width - rightSideX - iconSize - 40
      });
      wildDesc.x = rightSideX + iconSize + 12;
      wildDesc.y = rightSideY + 28;
      this.paytableContainer.addChild(wildDesc);
    }
  }

  private buildRules(x: number, y: number, width: number, height: number): void {
    this.rulesContainer.removeChildren();
    this.rulesContainer.x = 0;
    this.rulesContainer.y = 0;

    let currentY = y;
    const lineSpacing = 24;

    for (const line of RULES_TEXT) {
      const text = new Text(`• ${line}`, {
        fontFamily: 'Arial',
        fontSize: 16,
        fill: 0xffffff,
        wordWrap: true,
        wordWrapWidth: width
      });
      text.x = x;
      text.y = currentY;
      this.rulesContainer.addChild(text);
      currentY += lineSpacing + (text.height > lineSpacing ? 8 : 0);
    }
  }
}

