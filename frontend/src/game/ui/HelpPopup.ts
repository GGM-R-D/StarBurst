import { Modal } from './Modal';
import { TextStyle } from 'pixi.js';
import type { Application } from 'pixi.js';

/**
 * Help popup showing game rules and how to play.
 */
export class HelpPopup extends Modal {
  constructor(app?: Application) {
    super('Game Rules & Help', true, app); // Enable scrolling

    // Rules content
    this.addText('HOW TO PLAY', 0, 0, {
      fontSize: 22,
      fontWeight: 'bold',
      fill: '#ff00ff'
    });

    this.addText('', 0, 40); // Spacing

    this.addText('1. Set your bet amount using the bet controls', 0, 60);
    this.addText('2. Click the SPIN button to start the game', 0, 90);
    this.addText('3. Match symbols on active paylines to win', 0, 120);

    this.addText('', 0, 160); // Spacing

    this.addText('WILD SYMBOLS', 0, 180, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('• Wild symbols appear on reels 2, 3, and 4 only', 20, 210);
    this.addText('• When a wild appears, it expands to fill the reel', 20, 240);
    this.addText('• Expanding wilds trigger a respin', 20, 270);
    this.addText('• You can get up to 3 respins per spin', 20, 300);

    this.addText('', 0, 340); // Spacing

    this.addText('PAYLINES', 0, 360, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('The game has 10 fixed paylines.', 20, 390);
    this.addText('Wins pay BOTH left-to-right and right-to-left.', 20, 420);
    this.addText('All paylines are always active during gameplay.', 20, 450);

    this.addText('', 0, 490); // Spacing

    this.addText('WINNING COMBINATIONS', 0, 510, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('• 3 matching symbols = Small win', 20, 540);
    this.addText('• 4 matching symbols = Medium win', 20, 570);
    this.addText('• 5 matching symbols = Big win', 20, 600);
    this.addText('• Wild symbols substitute for other symbols', 20, 630);

    this.addText('', 0, 670); // Spacing

    this.addText('RESPINS', 0, 690, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('When a wild symbol appears on reels 2, 3, or 4,', 20, 720);
    this.addText('it expands to fill the entire reel. This triggers', 20, 750);
    this.addText('a respin where the expanded wild reel stays locked.', 20, 780);
    this.addText('You can get up to 3 respins per spin sequence.', 20, 810);
    this.addText('Each respin can create more expanding wilds,', 20, 840);
    this.addText('potentially leading to multiple respins in a row.', 20, 870);

    this.addText('', 0, 910); // Spacing

    this.addText('TIPS', 0, 930, {
      fontSize: 20,
      fontWeight: 'bold',
      fill: '#ffd700'
    });

    this.addText('• Watch for wild symbols on the middle reels', 20, 960);
    this.addText('• Expanding wilds can create multiple wins', 20, 990);
    this.addText('• Use the speed controls to adjust gameplay pace', 20, 1020);
    this.addText('• Check the paytable for symbol values', 20, 1050);
  }
}

