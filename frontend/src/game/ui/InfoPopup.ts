import { Modal } from './Modal';
import { TextStyle } from 'pixi.js';
import type { Application } from 'pixi.js';

/**
 * Info popup showing game information.
 */
export class InfoPopup extends Modal {
  constructor(app?: Application) {
    super('Game Information', true, app); // Enable scrolling

    // Game information content
    this.addText('STELLAR GEMS', 0, 0, {
      fontSize: 24,
      fontWeight: 'bold',
      fill: '#ff00ff'
    });

    this.addText('', 0, 40); // Spacing

    this.addText('Welcome to Stellar Gems!', 0, 60, {
      fontSize: 20,
      fontWeight: 'bold'
    });

    this.addText('', 0, 100); // Spacing

    this.addText('RTP: 96.10%', 0, 120, {
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('Return to Player percentage', 20, 150, {
      fontSize: 14,
      fill: '#cccccc'
    });

    this.addText('', 0, 180); // Spacing

    this.addText('Game Features:', 0, 200, {
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('• 5 reels, 3 rows, 10 paylines', 20, 230);
    this.addText('• Wild symbols expand and trigger respins', 20, 260);
    this.addText('• Up to 3 respins when wilds appear', 20, 290);
    this.addText('• High-quality graphics and animations', 20, 320);
    this.addText('• Smooth reel animations and effects', 20, 350);
    this.addText('• Multiple speed options for gameplay', 20, 380);

    this.addText('', 0, 420); // Spacing

    this.addText('PAYTABLE', 0, 440, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ff00ff'
    });

    this.addText('Payouts are multipliers of your bet per line', 20, 470, {
      fontSize: 14,
      fill: '#cccccc'
    });

    this.addText('', 0, 500); // Spacing

    // Paytable header
    this.addText('Symbol', 0, 520, {
      fontSize: 16,
      fontWeight: 'bold',
      fill: '#ffd700'
    });
    this.addText('3 Symbols', 150, 520, {
      fontSize: 16,
      fontWeight: 'bold',
      fill: '#ffd700'
    });
    this.addText('4 Symbols', 250, 520, {
      fontSize: 16,
      fontWeight: 'bold',
      fill: '#ffd700'
    });
    this.addText('5 Symbols', 350, 520, {
      fontSize: 16,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    // Paytable rows - Starburst math (multipliers)
    let yPos = 550;
    const rowHeight = 25;

    // Bar (highest paying symbol)
    this.addText('Bar', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('10x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('25x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('50x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Seven
    this.addText('Seven', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('5x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('12x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('25x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Orange
    this.addText('Orange Gem', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('2x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('5x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('12x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Green
    this.addText('Green Gem', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('1.6x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('4x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('10x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Red
    this.addText('Red Gem', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('1.4x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('3x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('8x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Blue
    this.addText('Blue Gem', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('1x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('2x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('5x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Purple
    this.addText('Purple Gem', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('1x', 150, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('2x', 250, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('5x', 350, yPos, { fontSize: 14, fill: '#ffffff' });
    yPos += rowHeight;

    // Wild (note: wild substitutes but doesn't pay directly)
    this.addText('Wild', 0, yPos, { fontSize: 14, fill: '#ffffff' });
    this.addText('Substitutes', 150, yPos, { fontSize: 14, fill: '#cccccc', fontStyle: 'italic' });
    this.addText('for all', 250, yPos, { fontSize: 14, fill: '#cccccc', fontStyle: 'italic' });
    this.addText('symbols', 350, yPos, { fontSize: 14, fill: '#cccccc', fontStyle: 'italic' });
    yPos += rowHeight + 10;

    this.addText('', 0, yPos); // Spacing

    this.addText('BETTING', 0, yPos + 20, {
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('Adjust your bet using the bet controls', 20, yPos + 50);
    this.addText('in the bottom control bar. The bet', 20, yPos + 80);
    this.addText('amount affects your potential winnings.', 20, yPos + 110);

    this.addText('', 0, yPos + 150); // Spacing

    this.addText('Version: 1.0.0', 0, yPos + 170, {
      fontSize: 14,
      fill: '#999999'
    });

    this.addText('© 2024 Game Studio', 0, yPos + 200, {
      fontSize: 14,
      fill: '#999999'
    });
  }
}

