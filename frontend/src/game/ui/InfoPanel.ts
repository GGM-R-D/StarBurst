// src/game/ui/InfoPanel.ts
import { Container, Graphics, Text, Sprite, Texture } from 'pixi.js';
import { SYMBOL_PAYTABLE, RULES_TEXT } from '../config/paytableConfig';
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
  private currentLineBet: number = 0.20; // Default line bet
  private _contentX: number = 0;
  private _contentY: number = 0;
  private _contentWidth: number = 0;
  private _contentHeight: number = 0;
  private paytableMask: Graphics;
  private rulesMask: Graphics;

  constructor(viewportWidth: number, viewportHeight: number, initialLineBet: number = 0.20, callbacks: InfoPanelCallbacks = {}) {
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

    const columnX = x;
    let rowY = y;

    const lineSpacing = 45; // Reduced spacing to fit more content
    const iconSize = 36; // Smaller icons to fit better

    for (const entry of SYMBOL_PAYTABLE) {
      if (entry.isWild) {
        // Wild gets its own section lower down
        continue;
      }

      // Icon - create sprite from symbol factory
      try {
        const sprite = createSymbolSprite(entry.id);
        sprite.width = iconSize;
        sprite.height = iconSize;
        sprite.x = columnX;
        sprite.y = rowY;
        sprite.anchor.set(0, 0);
        this.paytableContainer.addChild(sprite);
      } catch (error) {
        console.warn(`Failed to create sprite for ${entry.id}:`, error);
        // Create placeholder
        const placeholder = new Graphics();
        placeholder.beginFill(0x666666, 1);
        placeholder.drawRect(0, 0, iconSize, iconSize);
        placeholder.endFill();
        placeholder.x = columnX;
        placeholder.y = rowY;
        this.paytableContainer.addChild(placeholder);
      }

      // Name + pays text - calculate actual payout amounts based on line bet
      const pays = entry.pays;
      const lb = this.currentLineBet;
      const p3 = pays[3] != null ? (pays[3] * lb).toFixed(2) : '-';
      const p4 = pays[4] != null ? (pays[4] * lb).toFixed(2) : '-';
      const p5 = pays[5] != null ? (pays[5] * lb).toFixed(2) : '-';
      
      const textStr = `${entry.displayName.toUpperCase()}  –  5x ${p5}   4x ${p4}   3x ${p3}`;
      const text = new Text(textStr, {
        fontFamily: 'Arial',
        fontSize: 15, // Smaller font to fit better
        fill: 0xffffff
      });
      text.x = columnX + iconSize + 12;
      text.y = rowY + 2;
      this.paytableContainer.addChild(text);

      rowY += lineSpacing;
    }

    // Wild section below others
    const wildEntry = SYMBOL_PAYTABLE.find(e => e.isWild);
    if (wildEntry) {
      rowY += 10;

      try {
        const sprite = createSymbolSprite(wildEntry.id);
        sprite.width = iconSize;
        sprite.height = iconSize;
        sprite.x = columnX;
        sprite.y = rowY;
        sprite.anchor.set(0, 0);
        this.paytableContainer.addChild(sprite);
      } catch (error) {
        console.warn(`Failed to create sprite for ${wildEntry.id}:`, error);
        // Create placeholder
        const placeholder = new Graphics();
        placeholder.beginFill(0xffe66d, 1);
        placeholder.drawRect(0, 0, iconSize, iconSize);
        placeholder.endFill();
        placeholder.x = columnX;
        placeholder.y = rowY;
        this.paytableContainer.addChild(placeholder);
      }

      const wildTitle = new Text('WILD SYMBOL', {
        fontFamily: 'Arial',
        fontSize: 16, // Smaller font
        fill: 0xffe66d,
        fontWeight: 'bold'
      });
      wildTitle.x = columnX + iconSize + 12;
      wildTitle.y = rowY;
      this.paytableContainer.addChild(wildTitle);

      const wildDesc = new Text(wildEntry.description ?? '', {
        fontFamily: 'Arial',
        fontSize: 13, // Smaller font
        fill: 0xffffff,
        wordWrap: true,
        wordWrapWidth: width - iconSize - 24
      });
      wildDesc.x = columnX + iconSize + 12;
      wildDesc.y = rowY + 20;
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

