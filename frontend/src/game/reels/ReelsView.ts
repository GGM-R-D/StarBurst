import { Container, Graphics, BlurFilter } from 'pixi.js';
import gsap from 'gsap';

import { createSymbolSprite } from '@game/symbols/SymbolFactory';
import { type ReelLayout } from '@game/config/layoutConfig';
import type { SymbolId } from '@game/demo/SpinTypes';
import type { Win } from '@game/types/Win';
import type { AudioManager } from '@engine/audio/AudioManager';

export class ReelsView extends Container {
  private layout!: ReelLayout;

  // One container per reel (column)
  private reelContainers: Container[] = [];

  // Logical symbol IDs for the current visible 3x5 grid
  private symbolIds: SymbolId[][] = [];

  // Mask for the whole reel area - clips symbols that extend beyond visible area
  private reelsMask: Graphics | null = null;
  private spinBlurFilter: BlurFilter;
  private lockedReels: Set<number> = new Set();
  // Win highlight graphics
  private winHighlights: Graphics[] = [];
  private audioManager?: AudioManager;
  // Track active spin tweens for instant stop
  private activeSpinTweens: gsap.core.Tween[] = [];
  private activeBlurTween: gsap.core.Tween | null = null;
  private activeBlurDelayedCall: gsap.core.Timeline | null = null;
  private currentSpinTarget: SymbolId[][] | null = null;
  private spinResolveCallbacks: Array<() => void> = [];
  private animateSpinToResolve: (() => void) | null = null;
  private isStopped: boolean = false;
  // Callback for when spin animation completes (called once when all reels stop)
  // Must be async and awaited - win animations depend on this completing
  public onSpinAnimationComplete?: () => Promise<void>;

  constructor(audioManager?: AudioManager) {
    super();
    this.audioManager = audioManager;

    // Mask will be created on-demand in updateMask()
    // Never added as a child to prevent rendering

    this.spinBlurFilter = new BlurFilter();
    this.spinBlurFilter.blurX = 0;
    this.spinBlurFilter.blurY = 4;
  }

  /**
   * Initialize the reels with an initial result.
   * This is called once from GameApp after layout is created.
   */
  public initGrid(layout: ReelLayout, initialSymbols: SymbolId[][]): void {
    this.layout = layout;

    // Clear all children - mask is NOT a child, it's just used for masking
    super.removeChildren();

    this.reelContainers = [];
    this.symbolIds = [];
    this.lockedReels.clear();

    const { cols, rows, cellWidth, originX, originY, reelSpacing } = layout;

    for (let c = 0; c < cols; c++) {
      const reel = new Container();
      reel.x = originX + c * (cellWidth + reelSpacing) + cellWidth / 2;
      reel.y = originY;

      this.addChild(reel);
      this.reelContainers[c] = reel;

      this.symbolIds[c] = [];

      for (let r = 0; r < rows; r++) {
        const symbolId = initialSymbols[c][r];
        const sprite = this.createCellSprite(symbolId, r);

        reel.addChild(sprite);
        this.symbolIds[c][r] = symbolId;
      }
    }

    // Delay mask application to prevent color block on startup
    // Apply mask after first render frame
    this.mask = null; // Clear mask initially
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        // Apply mask after symbols have rendered
        this.updateMask();
      });
    });
  }

  private updateMask(): void {
    if (!this.layout) return;
    const { originX, originY, totalWidth, totalHeight, cellWidth, cellHeight } = this.layout;

    // Only apply mask if we have content to mask
    if (this.reelContainers.length === 0 || this.reelContainers[0].children.length === 0) {
      this.mask = null;
      if (this.reelsMask && this.reelsMask.parent) {
        // Safety check: ensure mask is never in display tree
        this.reelsMask.parent.removeChild(this.reelsMask);
      }
      return;
    }

    // Create mask Graphics on-demand - never add as child
    if (!this.reelsMask) {
      this.reelsMask = new Graphics();
    }

    // CRITICAL: Ensure mask is NEVER in the display tree
    if (this.reelsMask.parent) {
      this.reelsMask.parent.removeChild(this.reelsMask);
    }

    // Expand mask to prevent symbols from being cut off on sides and top/bottom
    // Add padding to allow symbol overflow
    const horizontalPadding = cellWidth * 0.3; // 30% padding on each side
    const verticalPadding = cellHeight * 0.2; // 20% padding on top and bottom
    
    // CRITICAL: Set properties BEFORE drawing to prevent any rendering
    this.reelsMask.renderable = false;
    this.reelsMask.visible = false;
    this.reelsMask.alpha = 0;
    this.reelsMask.eventMode = 'none';
    this.reelsMask.interactiveChildren = false;
    this.reelsMask.cullable = false; // Prevent culling
    
    // Draw mask shape
    this.reelsMask.clear();
    this.reelsMask.beginFill(0xffffff, 1);
    this.reelsMask.drawRect(
      originX - horizontalPadding, 
      originY - verticalPadding, 
      totalWidth + horizontalPadding * 2, 
      totalHeight + verticalPadding * 2
    );
    this.reelsMask.endFill();
    
    // CRITICAL: Re-apply properties after drawing to ensure no rendering
    this.reelsMask.renderable = false;
    this.reelsMask.visible = false;
    this.reelsMask.alpha = 0;
    
    // Apply mask - Graphics is NEVER added as a child, only used for masking
    this.mask = this.reelsMask;
  }

  /**
   * Recompute positions on resize.
   */
  public updateLayout(layout: ReelLayout): void {
    this.layout = layout;

    const { cols, rows, cellWidth, cellHeight, originX, originY, reelSpacing } = layout;

    for (let c = 0; c < cols; c++) {
      const reel = this.reelContainers[c];
      if (!reel) continue;

      reel.x = originX + c * (cellWidth + reelSpacing) + cellWidth / 2;
      reel.y = originY;

      for (let r = 0; r < rows; r++) {
        const sprite = reel.children[r] as any;
        if (!sprite) continue;
        // Keep position logic in sync with positionAndScaleSprite
        const { rowSpacing } = layout;
        sprite.x = 0;
        const cellTop = r * (cellHeight + rowSpacing);
        sprite.y = cellTop + cellHeight / 2;
      }
    }
    
    // Update mask on layout change
    this.updateMask();
  }


  /**
   * Helper: random demo symbols (client-side only).
   * Note: SYM_WILD is NOT included here - it only appears on reels 2-4 via game logic.
   */
  private getRandomDemoSymbolId(): SymbolId {
    const pool: SymbolId[] = [
      'SYM_BAR',
      'SYM_SEVEN',
      'SYM_ORANGE',
      'SYM_GREEN',
      'SYM_RED',
      'SYM_BLUE',
      'SYM_PURPLE'
    ];
    return pool[Math.floor(Math.random() * pool.length)];
  }

  /**
   * Animate reels to a new target result as a continuous multi-rotation strip spin.
   * target[col][row] (column-major) must contain REEL_ROWS entries per column.
   */
  public async animateSpinTo(target: SymbolId[][], speed: 1 | 2 | 3 = 1): Promise<void> {
    const { cols, rows, cellHeight, originY, rowSpacing } = this.layout;
    const promises: Promise<void>[] = [];
    
    // Reset stop flag
    this.isStopped = false;
    
    // Clear any existing tweens without finishing spins (just kill animations)
    this.clearActiveAnimations();
    
    // Store target for instant stop
    this.currentSpinTarget = target;
    this.spinResolveCallbacks = [];
    this.animateSpinToResolve = null;
    
    // Create main promise that can be resolved immediately on stop
    const mainPromise = new Promise<void>((resolve) => {
      this.animateSpinToResolve = resolve;
    });

    // Number of full 3-row "rotations" you see before the final stop.
    // Higher value = faster perceived spin speed for the same duration.
    const rotations = 14; // was 10

    // Map speed to blur and duration multiplier.
    const blurBySpeed: Record<1 | 2 | 3, number> = { 1: 6, 2: 9, 3: 12 };
    const durationBySpeed: Record<1 | 2 | 3, number> = { 1: 1.0, 2: 0.7, 3: 0.4 };
    const blurStrength = blurBySpeed[speed];
    const speedMul = durationBySpeed[speed];

    // Track the longest (delay + duration) across all reels for blur timing,
    // the first reel's time so blur is gone before it stops, and the last reel's
    // start delay so blur only appears once the last reel actually begins moving.
    let maxTotalTime = 0;
    let firstReelTotalTime = 0;
    let lastReelDelay = 0;

    for (let c = 0; c < cols; c++) {
      const targetColumn = target[c];
      const reel = this.reelContainers[c];

      // If this reel is locked (wild reel), do not spin or rebuild it.
      if (this.lockedReels.has(c)) {
        promises.push(Promise.resolve());
        continue;
      }

      // Kill any existing tweens on this reel before we animate it again
      gsap.killTweensOf(reel);

      // Preserve current visible column before we rebuild the strip
      const currentColumn = this.symbolIds[c] ? [...this.symbolIds[c]] : [];

      reel.removeChildren();

      // Number of extra "rotations" beyond the initial current 3
      const randomRowsCount = rotations * rows;

      // Strip structure (row indices within the strip) for downward motion:
      // 0 .. rows-1                    => final TARGET symbols
      // rows .. rows+randomRowsCount-1 => random symbols
      // rows+randomRowsCount .. rows+randomRowsCount+rows-1 => CURRENT visible symbols

      const stripTargetOffset = 0;
      const stripRandomOffset = rows;
      const stripCurrentOffset = rows + randomRowsCount;
      const totalRows = rows + randomRowsCount + rows;

      // 1) Final target column at the TOP of the strip
      for (let r = 0; r < rows; r++) {
        const symbolId = targetColumn[r];
        const sprite = this.createCellSprite(symbolId, stripTargetOffset + r);
        reel.addChild(sprite);
      }

      // 2) Random rows in the middle
      for (let i = 0; i < randomRowsCount; i++) {
        const randId = this.getRandomDemoSymbolId();
        const sprite = this.createCellSprite(randId, stripRandomOffset + i);
        reel.addChild(sprite);
      }

      // 3) Current visible symbols at the BOTTOM of the strip
      for (let r = 0; r < rows; r++) {
        const symbolId: SymbolId =
          currentColumn[r] ?? this.getRandomDemoSymbolId();
        const sprite = this.createCellSprite(symbolId, stripCurrentOffset + r);
        reel.addChild(sprite);
      }

      // --- SPIN DISTANCE FOR DOWNWARD MOTION ---
      //
      // Visible window top is at world Y = originY.
      //
      // At the START, we want the first \"current\" row (index = stripCurrentOffset)
      // to be visible on row 0:
      //   reel.y_start + (stripCurrentOffset * (cellHeight + rowSpacing) + h/2) = originY + h/2
      // => reel.y_start = originY - stripCurrentOffset * (cellHeight + rowSpacing)
      //
      // At the END, we want the first TARGET symbol at index 0 to be visible on row 0:
      //   reel.y_end + (0 * (cellHeight + rowSpacing) + h/2) = originY + h/2
      // => reel.y_end = originY

      const rowHeight = cellHeight + rowSpacing;
      const startY = originY - stripCurrentOffset * rowHeight;
      const endY = originY;

      reel.y = startY;

      // Smooth spin: base duration + small stagger per reel
      const baseDuration = 1.2; // can be tweaked
      const duration = (baseDuration + c * 0.15) * speedMul;
      const delay = c * 0.08;

      // Track the longest delay+duration for blur tween
      const totalTime = delay + duration;
      if (c === 0) {
        firstReelTotalTime = totalTime;
      }
      if (c === cols - 1) {
        lastReelDelay = delay;
      }
      if (totalTime > maxTotalTime) {
        maxTotalTime = totalTime;
      }

      const spinPromise = new Promise<void>((resolve) => {
        let resolved = false;
        const doResolve = () => {
          if (!resolved) {
            resolved = true;
            resolve();
          }
        };
        
        // Store resolve callback for instant stop (finishReelSpin will be called in stopSpin)
        this.spinResolveCallbacks.push(() => {
          doResolve();
        });
        
        const tween = gsap.to(reel, {
          y: endY,
          duration,
          delay,
          // Use linear easing so spin speed is visually constant
          ease: 'none',
          onComplete: () => {
            // After the spin completes, collapse back to the final 3 symbols
            // and reset reel.y to the idle position (originY).
            this.finishReelSpin(c, targetColumn, originY, rows);
            doResolve();
          }
        });
        
        // Track tween for instant stop
        this.activeSpinTweens.push(tween);
      });

      promises.push(spinPromise);
    }

    // We want blur to be fully gone BEFORE the FIRST reel stops,
    // and only start ONCE the LAST reel has begun spinning.
    // Use the first reel's total time as reference; if not available, fall back to max.
    const referenceTime = firstReelTotalTime || maxTotalTime;
    // Blur stays full for roughly the first 30% of the first reel's travel,
    // then fades out and is completely gone by ~80% of that time.
    const blurFadeEndTime = referenceTime * 0.8;
    const blurFadeDuration = referenceTime * 0.5;
    const blurFadeStartTime = Math.max(0, blurFadeEndTime - blurFadeDuration);

    // Start blur only once the LAST reel has actually started moving,
    // so earlier reels spin briefly with no blur visible.
    const blurStartDelay = lastReelDelay;

    const blurDelayedCall = gsap.delayedCall(blurStartDelay, () => {
      // Apply blur filter at full strength once reels are in motion
      this.spinBlurFilter.blurX = 0;
      this.spinBlurFilter.blurY = blurStrength;
      this.filters = [this.spinBlurFilter];

      // Keep full blur until blurFadeStartTime, then tween to 0.
      const fadeDelay = Math.max(0, blurFadeStartTime - blurStartDelay);
      const blurTween = gsap.to(this.spinBlurFilter, {
        blurY: 0,
        duration: blurFadeDuration,
        delay: fadeDelay,
        ease: 'linear',
        onComplete: () => {
          // Turn off filter once blur is effectively 0
          this.filters = null;
        }
      });
      
      // Track blur tween
      this.activeBlurTween = blurTween;
    });
    
    // Track delayed call
    this.activeBlurDelayedCall = blurDelayedCall as any;

    // Wait for either all promises to complete OR for stop to be called
    await Promise.race([
      Promise.all(promises),
      mainPromise
    ]);

    // Ensure blur is fully off after spin
    this.spinBlurFilter.blurY = 0;
    this.filters = null;
    
    // Clear tween tracking
    this.activeSpinTweens = [];
    this.activeBlurTween = null;
    this.activeBlurDelayedCall = null;
    this.currentSpinTarget = null;
    this.spinResolveCallbacks = [];
    this.animateSpinToResolve = null;
    
    // Stop spin sound immediately when reels stop spinning (before win animations)
    // This ensures the sound doesn't play during win animations
    if (this.audioManager) {
      this.audioManager.stop('spin');
      this.audioManager.play('stop');
    }
    
    // Call completion callback and wait for it to finish before resolving
    // This ensures win animations complete before the next spin/respin starts
    try {
      if (this.onSpinAnimationComplete) {
        // Use setTimeout to ensure this happens after all reel animations are truly complete
        await new Promise<void>((resolve) => {
          setTimeout(async () => {
            try {
              if (this.onSpinAnimationComplete) {
                await this.onSpinAnimationComplete();
              }
            } catch (err) {
              console.error('[ReelsView] Error in onSpinAnimationComplete:', err);
            } finally {
              resolve();
            }
          }, 50);
        });
      } else {
        console.warn('[ReelsView] animateSpinTo completed but onSpinAnimationComplete callback is not set');
      }
    } catch (err) {
      console.error('[ReelsView] Error in onSpinAnimationComplete:', err);
    }
  }

  /**
   * Clear active animations without finishing spins (used when starting new spin).
   */
  private clearActiveAnimations(): void {
    // Kill all active tweens
    for (const tween of this.activeSpinTweens) {
      gsap.killTweensOf(tween.targets());
    }
    
    if (this.activeBlurTween) {
      gsap.killTweensOf(this.spinBlurFilter);
    }
    
    if (this.activeBlurDelayedCall) {
      gsap.killTweensOf(this.activeBlurDelayedCall);
    }
    
    // Remove blur
    this.spinBlurFilter.blurY = 0;
    this.filters = null;
    
    // Clear tween tracking
    this.activeSpinTweens = [];
    this.activeBlurTween = null;
    this.activeBlurDelayedCall = null;
  }

  /**
   * Stop the reels instantly, completing animation to final position.
   * Returns true if spin was stopped, false if no spin was active.
   */
  public stopSpin(): boolean {
    if (!this.currentSpinTarget && !this.animateSpinToResolve) {
      return false; // No active spin
    }
    
    // Set stop flag
    this.isStopped = true;
    
    // Kill all active tweens
    for (const tween of this.activeSpinTweens) {
      gsap.killTweensOf(tween.targets());
    }
    
    if (this.activeBlurTween) {
      gsap.killTweensOf(this.spinBlurFilter);
    }
    
    if (this.activeBlurDelayedCall) {
      gsap.killTweensOf(this.activeBlurDelayedCall);
    }
    
    // Remove blur immediately
    this.spinBlurFilter.blurY = 0;
    this.filters = null;
    
    // Complete all reels to final position if we have a target
    if (this.currentSpinTarget) {
      const { cols, rows, originY, cellHeight, rowSpacing } = this.layout;
      
      for (let c = 0; c < cols; c++) {
        const reel = this.reelContainers[c];
        const targetColumn = this.currentSpinTarget[c];
        
        // Skip locked reels
        if (this.lockedReels.has(c)) {
          continue;
        }
        
        // Kill any tweens on this reel
        gsap.killTweensOf(reel);
        
        // Calculate how far the reel needs to move to reach final position
        const currentY = reel.y;
        const targetY = originY;
        const distance = Math.abs(targetY - currentY);
        
        // If already at target (or very close), just rebuild immediately
        if (distance < 1) {
          reel.y = originY;
          this.finishReelSpin(c, targetColumn, originY, rows);
        } else {
          // Complete the reel animation to final position smoothly
          // Duration based on distance, but capped for quick completion
          const duration = Math.min(distance / 2000, 0.3); // Max 0.3 seconds
          
          gsap.to(reel, {
            y: originY,
            duration: duration,
            ease: 'power2.out',
            onComplete: () => {
              // Once at final position, rebuild with final symbols
              this.finishReelSpin(c, targetColumn, originY, rows);
            }
          });
        }
      }
    }
    
    // Resolve all individual reel promises immediately
    for (const resolve of this.spinResolveCallbacks) {
      resolve();
    }
    
    // Resolve the main animateSpinTo promise immediately
    if (this.animateSpinToResolve) {
      this.animateSpinToResolve();
      this.animateSpinToResolve = null;
    }
    
    // Clear tween tracking (but keep currentSpinTarget until animation completes)
    this.activeSpinTweens = [];
    this.activeBlurTween = null;
    this.activeBlurDelayedCall = null;
    this.spinResolveCallbacks = [];
    
    // Note: onSpinAnimationComplete will be called when all reel animations finish
    // (handled in animateSpinTo after Promise.race resolves)
    
    return true; // Spin was stopped
  }
  
  /**
   * Request fast stop - accelerates reels to finish quickly.
   * This is called when player presses STOP button.
   */
  public requestFastStop(): void {
    this.stopSpin();
  }

  /**
   * Helper to finish a reel spin (collapse to final symbols).
   */
  private finishReelSpin(c: number, targetColumn: SymbolId[], originY: number, rows: number): void {
    const reel = this.reelContainers[c];
    
    // Remove all current children (the spinning strip)
    reel.removeChildren();
    
    // Rebuild with just the final 3 symbols
    for (let r = 0; r < rows; r++) {
      const symbolId = targetColumn[r];
      const sprite = this.createCellSprite(symbolId, r);
      reel.addChild(sprite);
      
      // Update the symbol IDs array
      if (!this.symbolIds[c]) this.symbolIds[c] = [];
      this.symbolIds[c][r] = symbolId;
    }
    
    // Ensure reel is at the correct final position
    reel.y = originY;
  }

  /**
   * Create a sprite for a given symbol and logical row index within the strip.
   * This method:
   * - Creates the AnimatedSprite via SymbolFactory.
   * - Scales it so the largest side is ~90% of cell size.
   * - Positions it at rowIndex using the current layout.
   */
  private createCellSprite(symbolId: SymbolId, rowIndex: number) {
    const sprite = createSymbolSprite(symbolId);
    sprite.anchor.set(0.5);
    this.positionAndScaleSprite(sprite, rowIndex);
    return sprite;
  }

  // Expand a given reel into a full wild column (3 stacked wild symbols).
  // Only reels 2, 3, 4 (indices 1, 2, 3) can be expanded.
  // Animates one row at a time, keeping the original wild symbol visible.
  public async expandWildReel(columnIndex: number): Promise<void> {
    if (!this.layout) return;
    
    // Safety check: only allow expansion on reels 2, 3, 4 (indices 1, 2, 3)
    // Reels 1 and 5 (indices 0 and 4) must NEVER be expanded
    if (columnIndex < 1 || columnIndex > 3) {
      console.warn(`Cannot expand wild on reel ${columnIndex + 1} - only reels 2, 3, 4 can have wilds`);
      return;
    }
    
    const reel = this.reelContainers[columnIndex];
    if (!reel) {
      console.warn(`[ReelsView] No reel container found for column ${columnIndex}`);
      return;
    }

    const { rows } = this.layout;

    // Find which row currently has the wild symbol
    // Only SYM_WILD exists as expanding wild (SYM_WILD_STAR removed)
    let wildRowIndex = -1;
    let existingWildSprite: any = null;
    
    console.info(`[ReelsView] Expanding wild on reel ${columnIndex + 1}`);
    console.info(`[ReelsView] Current symbolIds:`, this.symbolIds[columnIndex]);
    console.info(`[ReelsView] Reel children count:`, reel.children.length);
    
    // First, check symbolIds array
    for (let r = 0; r < rows; r++) {
      const symbolId = this.symbolIds[columnIndex]?.[r];
      if (symbolId === 'SYM_WILD') {
        wildRowIndex = r;
        // Find the corresponding sprite in the reel
        if (r < reel.children.length) {
          existingWildSprite = reel.children[r];
        }
        console.info(`[ReelsView] Found wild in symbolIds at row ${r}`);
        break;
      }
    }

    // If not found in symbolIds, check the actual sprites (they might be set but symbolIds not updated)
    if (wildRowIndex === -1) {
      for (let r = 0; r < rows && r < reel.children.length; r++) {
        const sprite = reel.children[r];
        // Check if sprite has a wild texture or if we can identify it as wild
        // For now, we'll check symbolIds again with a different approach
        if (this.symbolIds[columnIndex]?.[r] === 'SYM_WILD') {
          wildRowIndex = r;
          existingWildSprite = sprite;
          console.info(`[ReelsView] Found wild sprite at row ${r}`);
          break;
        }
      }
    }

    // If still not found, check if grid shows a wild (fallback)
    if (wildRowIndex === -1) {
      const currentGrid = this.getCurrentGrid();
      if (currentGrid[columnIndex]) {
        for (let r = 0; r < currentGrid[columnIndex].length; r++) {
          if (currentGrid[columnIndex][r] === 'SYM_WILD') {
            wildRowIndex = r;
            if (r < reel.children.length) {
              existingWildSprite = reel.children[r];
            }
            console.info(`[ReelsView] Found wild in grid at row ${r}`);
            break;
          }
        }
      }
    }

    // If still not found, default to middle row and create wild there
    if (wildRowIndex === -1) {
      wildRowIndex = Math.floor(rows / 2);
      console.warn(`[ReelsView] No wild found, defaulting to middle row ${wildRowIndex} and creating wild`);
    }

    // Store existing sprites that are NOT the wild (we'll replace them)
    const spritesToReplace: Array<{ row: number; sprite: any }> = [];
    for (let r = 0; r < rows && r < reel.children.length; r++) {
      if (r !== wildRowIndex) {
        spritesToReplace.push({ row: r, sprite: reel.children[r] });
      }
    }

    // Keep the existing wild symbol visible - no animation needed

    // Animate expansion: replace each non-wild row one at a time
    for (let i = 0; i < spritesToReplace.length; i++) {
      const { row, sprite } = spritesToReplace[i];
      
      // Create new wild symbol
      const newWildSprite = this.createCellSprite('SYM_WILD', row);
      
      // Position it at the same location as the old sprite
      const originalX = sprite.x;
      const originalY = sprite.y;
      newWildSprite.x = originalX;
      newWildSprite.y = originalY;
      
      // Start invisible
      newWildSprite.alpha = 0;
      
      // Add to reel
      reel.addChild(newWildSprite);

      // Update logical symbol IDs
      if (!this.symbolIds[columnIndex]) this.symbolIds[columnIndex] = [];
      this.symbolIds[columnIndex][row] = 'SYM_WILD';

      // Animate old sprite fading out and new sprite fading in
      const delay = i * 0.6; // 600ms delay between each row (more dramatic)
      
      // Fade out old sprite
      gsap.to(sprite, {
        alpha: 0,
        duration: 0.5,
        delay: delay,
        ease: 'power2.in',
        onComplete: () => {
          reel.removeChild(sprite);
        }
      });

      // Fade in new wild sprite
      gsap.to(newWildSprite, {
        alpha: 1,
        duration: 0.6,
        delay: delay,
        ease: 'power2.out'
      });
      
      // Play expanding sound for each symbol
      if (this.audioManager) {
        gsap.delayedCall(delay, () => {
          this.audioManager?.play('expanding');
        });
      }
      
      // Subtle shake animation when expanding
      gsap.to(newWildSprite, {
        x: originalX + 3,
        duration: 0.05,
        delay: delay,
        ease: 'power2.out',
        yoyo: true,
        repeat: 5
      });
      gsap.to(newWildSprite, {
        y: originalY + 2,
        duration: 0.05,
        delay: delay,
        ease: 'power2.out',
        yoyo: true,
        repeat: 5
      });
    }

    // If we didn't find an existing wild, create one for the wildRowIndex
    if (!existingWildSprite && wildRowIndex >= 0) {
      const sprite = this.createCellSprite('SYM_WILD', wildRowIndex);
      sprite.alpha = 0;
      reel.addChild(sprite);
      
      if (!this.symbolIds[columnIndex]) this.symbolIds[columnIndex] = [];
      this.symbolIds[columnIndex][wildRowIndex] = 'SYM_WILD';

      const delay = spritesToReplace.length * 0.6;
      const originalX = sprite.x;
      const originalY = sprite.y;
      gsap.to(sprite, {
        alpha: 1,
        duration: 0.6,
        delay: delay,
        ease: 'power2.out'
      });
      
      // Play expanding sound
      if (this.audioManager) {
        gsap.delayedCall(delay, () => {
          this.audioManager?.play('expanding');
        });
      }
      
      // Subtle shake animation
      gsap.to(sprite, {
        x: originalX + 3,
        duration: 0.05,
        delay: delay,
        ease: 'power2.out',
        yoyo: true,
        repeat: 5
      });
      gsap.to(sprite, {
        y: originalY + 2,
        duration: 0.05,
        delay: delay,
        ease: 'power2.out',
        yoyo: true,
        repeat: 5
      });
    }

    // Wait for animation to complete
    const totalAnimationTime = spritesToReplace.length * 0.6 + 0.6;
    await new Promise<void>((resolve) => setTimeout(resolve, totalAnimationTime * 1000));
  }

  public async expandWildReels(columns: number[]): Promise<void> {
    // Expand all reels in parallel
    await Promise.all(columns.map((c) => this.expandWildReel(c)));
  }

  public lockReels(indices: number[]): void {
    indices.forEach((i) => this.lockedReels.add(i));
  }

  public clearLockedReels(): void {
    this.lockedReels.clear();
  }

  private setSpinBlur(enabled: boolean, strength: number = 4): void {
    if (enabled) {
      this.spinBlurFilter.blurY = strength;
      this.filters = [this.spinBlurFilter];
    } else {
      this.filters = null;
    }
  }

  private positionAndScaleSprite(sprite: any, rowIndex: number): void {
    const { cellWidth, cellHeight, rowSpacing } = this.layout;

    // Size symbols bigger - use full cell dimensions
    const targetSize = Math.min(cellWidth * 1.4, cellHeight * 1.3); // Much larger symbols

    // Compute from bounds when available; fall back to design size if needed.
    let baseW = 1000;
    let baseH = 1000;
    const bounds = sprite.getLocalBounds();
    if (bounds.width > 0 && bounds.height > 0) {
      baseW = bounds.width;
      baseH = bounds.height;
    }

    const maxSide = Math.max(baseW, baseH);
    const scale = targetSize / maxSide;

    sprite.scale.set(scale);

    sprite.x = 0;
    // Position symbol centered in cell with vertical padding
    const cellTop = rowIndex * (cellHeight + rowSpacing);
    sprite.y = cellTop + cellHeight / 2; // Centered in cell (padding is handled by size reduction)
  }

  /**
   * Get the current symbol grid state.
   */
  public getCurrentGrid(): SymbolId[][] {
    // Return a copy of the current symbol grid
    return this.symbolIds.map(col => [...col]);
  }

  /**
   * Clear all win highlights.
   */
  public clearWins(): void {
    // Remove all win highlight graphics and kill animations
    for (const highlight of this.winHighlights) {
      gsap.killTweensOf(highlight);
      if (highlight.parent) {
        highlight.parent.removeChild(highlight);
      }
      highlight.destroy();
    }
    this.winHighlights = [];
  }

  /**
   * Get a unique color for each payline based on its ID.
   * Returns an array of [outerGlowColor, middleGlowColor, mainLineColor]
   * Matches PaylineView color scheme.
   */
  private getPaylineColors(lineId: number): [number, number, number] {
    // Color palette - each payline gets a unique vibrant color
    const colorPalette: Array<[number, number, number]> = [
      [0xff4dff, 0xff66ff, 0xff00ff], // Magenta/Pink (original)
      [0x4dff4d, 0x66ff66, 0x00ff00], // Green
      [0x4d4dff, 0x6666ff, 0x0000ff], // Blue
      [0xff4d4d, 0xff6666, 0xff0000], // Red
      [0xffff4d, 0xffff66, 0xffff00], // Yellow
      [0xff4dff, 0xff66ff, 0xff00ff], // Cyan
      [0xff994d, 0xffaa66, 0xff8800], // Orange
      [0x4dffff, 0x66ffff, 0x00ffff], // Aqua
      [0xff4d99, 0xff66aa, 0xff0088], // Rose
      [0x994dff, 0xaa66ff, 0x8800ff], // Purple
    ];

    // Use modulo to cycle through colors if there are more paylines than colors
    const colorIndex = (lineId - 1) % colorPalette.length;
    return colorPalette[colorIndex];
  }

  /**
   * Show win highlights for the given wins.
   * Returns the line IDs that won for payline indicator highlighting.
   */
  public showWins(wins: Win[]): number[] {
    // Clear existing highlights first
    this.clearWins();

    if (!this.layout || wins.length === 0) return [];

    const { cellWidth, cellHeight, originX, originY, reelSpacing, rowSpacing } = this.layout;

    // Track which line IDs have wins
    const winningLineIds = new Set<number>();

    // Calculate symbol size (matching positionAndScaleSprite logic)
    const symbolSize = Math.min(cellWidth * 1.4, cellHeight * 1.3);
    
    // Create highlight graphics for each win
    for (const win of wins) {
      winningLineIds.add(win.lineId);

      // Get payline colors for this win (matching the payline color)
      const [outerGlowColor, middleGlowColor, mainLineColor] = this.getPaylineColors(win.lineId);

      // Create a graphics object to highlight winning positions
      const highlight = new Graphics();
      
      // Draw highlights for each position in the win
      for (const pos of win.positions) {
        // Calculate symbol center position (matching how symbols are positioned)
        const symbolCenterX = originX + pos.reel * (cellWidth + reelSpacing) + cellWidth / 2;
        const symbolCenterY = originY + pos.row * (cellHeight + rowSpacing) + cellHeight / 2;
        
        // Calculate highlight box position (centered on symbol, matching symbol size)
        const highlightX = symbolCenterX - symbolSize / 2;
        const highlightY = symbolCenterY - symbolSize / 2;
        
        // Draw a semi-transparent overlay (using payline color)
        highlight.beginFill(mainLineColor, 0.3);
        highlight.drawRect(highlightX, highlightY, symbolSize, symbolSize);
        highlight.endFill();
        
        // Draw a border (using payline main color, matching the payline)
        highlight.lineStyle(2, mainLineColor, 0.8);
        highlight.drawRect(highlightX, highlightY, symbolSize, symbolSize);
      }
      
      this.addChild(highlight);
      this.winHighlights.push(highlight);
      
      // Add pulsing animation to the highlight
      highlight.alpha = 0.5;
      gsap.to(highlight, {
        alpha: 1.0,
        duration: 0.6,
        yoyo: true,
        repeat: -1,
        ease: 'power2.inOut'
      });
    }

    return Array.from(winningLineIds);
  }
}

