import type { Application } from 'pixi.js';
import { createLayers, type GameLayers } from '@engine/renderer/Layers';
import { GameStateMachine } from '@engine/state/GameStateMachine';
import { GameState } from '@engine/state/GameState';
import { eventBus } from '@engine/events/EventBus';
import { GameEvent } from '@engine/events/events';
import { rgsClient } from '@network/rgsClient';
import type { StartResponse, PlayResponse, BalanceRequest } from '@network/types';
import { parseGameUrlParams } from '@network/urlParams';
import { BackgroundView } from '@game/background/BackgroundView';
import { ReelsView } from '@game/reels/ReelsView';
import { ReelFrameView } from '@game/ui/ReelFrameView';
import { TopBar } from '@game/ui/TopBar';
import { BottomBar } from '@game/ui/BottomBar';
import { WinPanel } from '@game/ui/WinPanel';
import { PaylineIndicators } from '@game/ui/PaylineIndicators';
import { PaylineView } from '@game/ui/PaylineView';
import { ReelDividers } from '@game/ui/ReelDividers';
import { WinCounter } from '@game/ui/WinCounter';
import { InfoPopup } from '@game/ui/InfoPopup';
import { InfoPanel } from '@game/ui/InfoPanel';
import { HelpPopup } from '@game/ui/HelpPopup';
import { SettingsPopup } from '@game/ui/SettingsPopup';
import { SoundPopup } from '@game/ui/SoundPopup';
import { AutoSpinPopup } from '@game/ui/AutoSpinPopup';
import { BetPopup } from '@game/ui/BetPopup';
import { computeReelLayout } from '@game/config/ReelLayout';
import type { SpinResult, SymbolId } from '@game/demo/SpinTypes';
import { PAYLINES } from '@game/config/paylines';
import { AudioManager } from '@engine/audio/AudioManager';
import { SOUND_CONFIG } from '@game/config/soundConfig';

export class GameApp extends GameStateMachine {
  private app: Application;
  private layers: GameLayers;
  private sessionId: string | null = null;
  private urlParams = parseGameUrlParams();
  private backgroundView: BackgroundView;
  private reelsView: ReelsView;
  private reelFrameView: ReelFrameView;
  private topBar: TopBar;
  private bottomBar: BottomBar;
  private winPanel: WinPanel;
  private paylineIndicators: PaylineIndicators;
  private paylineView: PaylineView;
  private reelDividers: ReelDividers;
  private winCounter: WinCounter;
  private infoPopup: InfoPopup;
  private infoPanel!: InfoPanel;
  private helpPopup: HelpPopup;
  private settingsPopup: SettingsPopup;
  private soundPopup: SoundPopup;
  private autoSpinPopup: AutoSpinPopup;
  private betPopup: BetPopup;
  private audioManager: AudioManager;
  private spinSpeed: 1 | 2 | 3 = 1;
  private reelsInitialized = false;
  private readonly MAX_RESPINS = 3;
  private balance = 0;
  private autoSpinCount: number = 0;
  private isAutoSpinning: boolean = false;
  // Sticky wild reels tracking for respin feature (0-based reel indexes: 1,2,3 → reels 2–4)
  private stickyWildReels: Set<number> = new Set();
  
  // Spin state flags
  private isSpinning = false;          // true while any spin or respin is in progress
  private inRespinFeature = false;     // true while we are inside a respin sequence
  private pendingRespins = 0;          // how many respins remain
  private isWinAnimating = false;      // true while win highlights / paylines are being shown
  // Backend wins from RGS response (no local calculation)
  private currentBackendWins?: Array<{
    lineId: number;
    paylineIndex: number;
    symbol: string;
    count: number;
    payout: number;
    positions: Array<{ reel: number; row: number }>;
  }>;
  private currentBackendTotalWin?: number;
  // Initial grid before wild expansion (for detecting new wilds)
  private currentInitialGrid?: SymbolId[][];

  constructor(app: Application) {
    super();
    this.app = app;
    this.layers = createLayers(this.app.stage);
    this.setState(GameState.BOOT);

    // Initialize audio manager
    this.audioManager = new AudioManager();
    this.initializeSounds();

    // Create views
    this.backgroundView = new BackgroundView();
    this.reelsView = new ReelsView(this.audioManager);
    this.reelFrameView = new ReelFrameView();
    
    // Wire ReelsView completion callback - this is called ONCE when animation finishes
    // This will be used during playRound to wait for completion
    this.reelsView.onSpinAnimationComplete = async () => {
      const shouldContinue = await this.onSpinAnimationComplete();
      // The handleSpinButtonClick loop will handle continuing respins
    };
    
    // Create popups
    this.infoPopup = new InfoPopup(this.app);
    this.helpPopup = new HelpPopup(this.app);
    
    // InfoPanel will be initialized in updateLayout after we know viewport size
    this.settingsPopup = new SettingsPopup({
      onQualityChange: (quality) => {
        console.log('Quality changed to:', quality);
        // TODO: Implement quality settings
      },
      onFullscreenToggle: () => {
        console.log('Fullscreen toggled');
        // TODO: Implement fullscreen toggle
      }
    });
    this.soundPopup = new SoundPopup({
      onMasterVolumeChange: (volume) => {
        this.audioManager.setMasterVolume(volume);
      },
      onMusicVolumeChange: (volume) => {
        this.audioManager.setMusicVolume(volume);
      },
      onSFXVolumeChange: (volume) => {
        this.audioManager.setSFXVolume(volume);
      },
      onSoundToggle: (enabled) => {
        this.audioManager.setSoundEnabled(enabled);
      }
    });
    this.autoSpinPopup = new AutoSpinPopup({
      onAutoSpinStart: (count) => {
        this.startAutoSpin(count);
      }
    }, this.app);

    // Sync sound popup with audio manager initial values
    this.syncSoundPopupWithAudioManager();

    this.topBar = new TopBar({
      onInfo: () => {
        this.audioManager.play('click');
        this.showInfoPopup();
      },
      onHelp: () => {
        this.audioManager.play('click');
        this.showHelpPopup();
      },
      onSettings: () => {
        this.audioManager.play('click');
        this.showSettingsPopup();
      },
      onSoundToggle: () => {
        this.audioManager.play('click');
        this.showSoundPopup();
      },
      onFeature: () => {
        this.audioManager.play('click');
        void this.handleFeatureButtonClick();
      }
    });
    this.bottomBar = new BottomBar({
      onSpin: () => {
        void this.handleSpinButtonClick();
      },
      onSpeedChange: (speed) => {
        this.audioManager.play('click');
        this.spinSpeed = speed;
      },
      onBetDecrease: () => {
        this.audioManager.play('click');
        // TODO: Implement bet decrease
      },
      onBetIncrease: () => {
        this.audioManager.play('click');
        // TODO: Implement bet increase
      },
      onButtonClick: () => {
        this.audioManager.play('click');
      },
      onAutoSpin: () => {
        this.audioManager.play('click');
        this.showAutoSpinPopup();
      },
      onBetClick: () => {
        this.audioManager.play('click');
        this.showBetPopup();
      },
      onBetChanged: (lineBet, totalBet) => {
        console.info('[GameApp] Bet changed. Line bet:', lineBet, 'Total bet:', totalBet);
        // Update InfoPanel if it's open
        if (this.infoPanel && this.infoPanel.visible) {
          this.infoPanel.updateForBet(lineBet);
        }
      }
    });
    // Initialize bet popup after bottomBar is created
    this.betPopup = new BetPopup({
      onBetSelected: (bet: number) => {
        this.audioManager.play('click');
        this.bottomBar.setBet(bet);
        // Don't call close() here - selectBet() or close() already handles closing
      },
      onClose: () => {
        this.audioManager.play('click');
      }
    }, this.bottomBar.getCurrentBet());
    this.winPanel = new WinPanel();
    this.paylineIndicators = new PaylineIndicators();
    this.paylineView = new PaylineView();
    this.reelDividers = new ReelDividers();
    this.winCounter = new WinCounter();

    // Add to layers - order matters for z-index
    this.layers.background.addChild(this.backgroundView);
    
    // UI layer: top bar
    this.layers.ui.addChild(this.topBar);
    
    // Reels layer: dividers (behind symbols), frame, then symbols
    this.layers.reels.addChild(this.reelDividers);
    this.layers.reels.addChild(this.reelFrameView);
    this.layers.reels.addChild(this.reelsView);
    
    // Payline indicators (above reels but below win overlays)
    this.layers.reels.addChild(this.paylineIndicators);
    // Payline lines (above indicators so they connect to them)
    this.layers.reels.addChild(this.paylineView);
    
    // Win counter (in fx layer, above everything except modals)
    this.layers.fx.addChild(this.winCounter);
    
    // UI layer: win panel and bottom bar
    this.layers.ui.addChild(this.winPanel);
    this.layers.ui.addChild(this.bottomBar);

    // Modals layer: popups
    this.layers.modals.addChild(this.infoPopup);
    this.layers.modals.addChild(this.helpPopup);
    this.layers.modals.addChild(this.settingsPopup);
    this.layers.modals.addChild(this.soundPopup);
    this.layers.modals.addChild(this.autoSpinPopup);
    this.layers.modals.addChild(this.betPopup);
    
    // InfoPanel will be added in updateLayout after initialization

    // Set initial state
    this.bottomBar.setSpinEnabled(true);
    this.bottomBar.setBalance(this.balance);
    this.spinSpeed = this.bottomBar.getCurrentSpeed();

    // Setup click-anywhere to cancel win animations
    this.setupWinAnimationCancellation();

    // Setup initial layout - fixed dimensions, no resize handling
    this.updateLayout().catch(err => {
      console.error('Error updating layout:', err);
    });
  }

  /**
   * Setup click-anywhere handler to cancel win animations.
   * Clicking anywhere on screen during win animations will skip them.
   */
  private setupWinAnimationCancellation(): void {
    // Make stage interactive so it can receive click events
    this.app.stage.eventMode = 'static';
    this.app.stage.hitArea = this.app.screen; // Make entire screen clickable
    
    // Helper to check if target is part of an interactive UI element
    const isInteractiveElement = (target: any): boolean => {
      if (!target) return false;
      
      let current: any = target;
      // Walk up the parent chain to check if we're inside a UI container
      while (current) {
        if (
          current === this.bottomBar ||
          current === this.topBar ||
          current === this.infoPopup ||
          current === this.helpPopup ||
          current === this.settingsPopup ||
          current === this.soundPopup ||
          current === this.autoSpinPopup ||
          // Check if element itself is interactive (has eventMode set)
          (current.eventMode && current.eventMode !== 'none' && current !== this.app.stage)
        ) {
          return true;
        }
        current = current.parent;
      }
      return false;
    };
    
    // Add click handler to stage (use pointertap for better compatibility)
    this.app.stage.on('pointerdown', (event: any) => {
      // Only cancel if win animations are in progress
      if (this.isWinAnimating || this.paylineView.getIsAnimating()) {
        // Don't cancel if clicking on interactive elements (buttons, popups, etc.)
        if (isInteractiveElement(event.target)) {
          // Click is on a button or popup, let it handle normally
          return;
        }
        
        console.info('[GameApp] Click detected during win animations - cancelling...');
        // Cancel win animations
        this.reelsView.clearWins();
        this.paylineIndicators.clearHighlights();
        this.paylineView.clearPaylines();
        this.winPanel.hideWin();
        this.bottomBar.setWin(0);
        this.isWinAnimating = false; // Reset flag
        
        // Stop event propagation to prevent any other handlers
        event.stopPropagation();
      }
    });
  }

  private async updateLayout(): Promise<void> {
    // Use fixed game dimensions (not responsive)
    const { GAME_WIDTH, GAME_HEIGHT } = await import('@engine/renderer/PixiAppFactory');
    const width = GAME_WIDTH; // Fixed: 1920px
    const height = GAME_HEIGHT; // Fixed: 1080px

    // Update background first (it's the base layer)
    this.backgroundView.resize(width, height);

    // Compute reel layout (centered)
    const layout = computeReelLayout(width, height);

    // Update top bar (pass layout for logo positioning)
    this.topBar.updateLayout(width, height, layout);

    // Initialize reels once with empty grid (will be populated after first spin from RGS)
    if (!this.reelsInitialized) {
      // Preload textures before initializing reels to ensure symbols show immediately
      const { preloadSymbolTextures } = await import('@game/symbols/SymbolFactory');
      await preloadSymbolTextures();
      
      // Initialize with varied symbols for a better visual appearance on startup
      // Use different symbols across the grid instead of all the same symbol
      const availableSymbols: SymbolId[] = [
        'SYM_BAR',
        'SYM_SEVEN',
        'SYM_ORANGE',
        'SYM_GREEN',
        'SYM_RED',
        'SYM_BLUE',
        'SYM_PURPLE'
      ];
      const initialGrid: SymbolId[][] = [];
      for (let c = 0; c < layout.cols; c++) {
        initialGrid[c] = [];
        for (let r = 0; r < layout.rows; r++) {
          // Use different symbols for variety - cycle through available symbols
          const symbolIndex = (c * layout.rows + r) % availableSymbols.length;
          initialGrid[c][r] = availableSymbols[symbolIndex];
        }
      }
      this.reelsView.initGrid(layout, initialGrid);
      this.reelsInitialized = true;
    } else {
      // Update layout on resize
      this.reelsView.updateLayout(layout);
    }

    // Update frame
    this.reelFrameView.updateFrameSize();

    // Update reel dividers
    this.reelDividers.updateLayout(layout);

    // Update payline indicators
    this.paylineIndicators.updateLayout(layout);
    
    // Update payline view with indicator positions
    const indicatorPositions = this.paylineIndicators.getIndicatorPositions();
    const { originX, cellWidth, cols, reelSpacing } = layout;
    const leftX = originX - 80;
    const rightX = originX + cols * cellWidth + (cols - 1) * reelSpacing + 60;
    
    // Convert positions to the format PaylineView expects
    const positionsMap = new Map<number, { leftY: number; rightY: number }>();
    indicatorPositions.forEach((pos, lineId) => {
      positionsMap.set(lineId, { leftY: pos.leftY, rightY: pos.rightY });
    });
    
    this.paylineView.updateLayout(layout, leftX, rightX, positionsMap);

    // Update win panel (positioned below reels, not overlapping) - matching EXACT specification
    const reelsY = layout.originY;
    const reelsHeight = layout.rows * layout.cellHeight;
    this.winPanel.updateLayout(layout, reelsY, reelsHeight, width);

    // Update bottom bar
    this.bottomBar.updateLayout(width, height);

    // Update win counter
    this.winCounter.updateLayout(width, height);

    // Initialize InfoPanel if not already created
    if (!this.infoPanel) {
      const lineBet = this.bottomBar ? this.bottomBar.getCurrentLineBet() : 0.20;
      this.infoPanel = new InfoPanel(width, height, lineBet, {
        onClose: () => {
          this.closeInfoPanel();
        }
      });
      this.layers.modals.addChild(this.infoPanel);
    }

    // Update modals
    this.infoPopup.updateLayout(width, height);
    this.helpPopup.updateLayout(width, height);
    this.settingsPopup.updateLayout(width, height);
    this.soundPopup.updateLayout(width, height);
    this.autoSpinPopup.updateLayout(width, height);
    
    // Update InfoPanel layout
    this.infoPanel.updateLayout(width, height);
  }

  private showInfoPopup(): void {
    // Use new InfoPanel instead of old InfoPopup
    this.openInfoPanel('paytable');
  }
  
  public openInfoPanel(initialTab: 'paytable' | 'rules' = 'paytable'): void {
    // Don't allow opening while spinning
    if (this.isSpinning) {
      return;
    }
    
    // Close other popups (without applying bet changes)
    this.helpPopup.close();
    this.settingsPopup.close();
    this.soundPopup.close();
    this.autoSpinPopup.close();
    if (this.betPopup && this.betPopup.visible) {
      this.betPopup.closeWithoutApplying();
    }
    
    // Disable spin button while panel is open
    this.bottomBar.setSpinEnabled(false);
    
    // Update InfoPanel with current line bet before showing (without triggering bet changes)
    const lineBet = this.bottomBar.getCurrentLineBet();
    this.infoPanel.updateForBet(lineBet);
    
    // Show InfoPanel
    this.infoPanel.show(initialTab);
  }
  
  public closeInfoPanel(): void {
    this.infoPanel.hide();
    
    // Re-enable spin button if game is idle
    if (!this.isSpinning) {
      this.bottomBar.setSpinEnabled(true);
    }
  }

  private showHelpPopup(): void {
    // Close other popups
    this.infoPopup.close();
    this.settingsPopup.close();
    this.soundPopup.close();
    this.autoSpinPopup.close();
    if (this.betPopup && this.betPopup.visible) {
      this.betPopup.closeWithoutApplying();
    }
    // Show help popup
    this.helpPopup.show();
  }

  private showSettingsPopup(): void {
    // Close other popups
    this.infoPopup.close();
    this.helpPopup.close();
    this.soundPopup.close();
    this.autoSpinPopup.close();
    if (this.betPopup && this.betPopup.visible) {
      this.betPopup.closeWithoutApplying();
    }
    // Show settings popup
    this.settingsPopup.show();
  }

  private showSoundPopup(): void {
    // Close other popups
    this.infoPopup.close();
    this.helpPopup.close();
    this.settingsPopup.close();
    this.autoSpinPopup.close();
    if (this.betPopup && this.betPopup.visible) {
      this.betPopup.closeWithoutApplying();
    }
    // Sync with current audio manager values
    this.syncSoundPopupWithAudioManager();
    // Show sound popup
    this.soundPopup.show();
  }

  private showAutoSpinPopup(): void {
    // Close other popups
    this.infoPopup.close();
    this.helpPopup.close();
    this.settingsPopup.close();
    this.soundPopup.close();
    if (this.betPopup && this.betPopup.visible) {
      this.betPopup.closeWithoutApplying();
    }
    // Show auto spin popup
    this.autoSpinPopup.show();
  }

  private showBetPopup(): void {
    // Close other popups first
    this.infoPopup.close();
    this.helpPopup.close();
    this.settingsPopup.close();
    this.soundPopup.close();
    this.autoSpinPopup.close();
    
    // Remove old bet popup if it exists
    if (this.betPopup && this.betPopup.parent) {
      this.layers.modals.removeChild(this.betPopup);
    }
    
    // Create new bet popup with current bet
    this.betPopup = new BetPopup({
      onBetSelected: (bet: number) => {
        this.audioManager.play('click');
        this.bottomBar.setBet(bet);
        // Ensure balance is displayed correctly (refresh display)
        this.bottomBar.setBalance(this.balance);
      },
      onClose: () => {
        this.audioManager.play('click');
      }
    }, this.bottomBar.getCurrentBet());
    
    // Position popup relative to bet button
    const betButtonPos = this.bottomBar.getBetButtonPosition();
    this.betPopup.setBetButtonPosition(betButtonPos);
    
    // Add to modals layer
    this.layers.modals.addChild(this.betPopup);
    
    // Update layout to ensure proper positioning
    this.betPopup.updateLayout(this.app.screen.width, this.app.screen.height);
    
    // Show bet popup
    this.betPopup.show();
  }

  private syncSoundPopupWithAudioManager(): void {
    // This will be implemented when SoundPopup has getter methods
    // For now, the popup initializes with default values
  }

  private initializeSounds(): void {
    // Register all sounds
    Object.entries(SOUND_CONFIG).forEach(([key, config]) => {
      this.audioManager.registerSound(key, config);
    });

    // Start background music
    this.audioManager.playMusic('background-music');
    
    // Debug: Log registered sounds
    console.info('[GameApp] Sounds registered:', Object.keys(SOUND_CONFIG));
  }

  async init(): Promise<void> {
    try {
      console.info('[GameApp] Initializing game...');
      console.info('[GameApp] RGS URL:', window.config?.SERVER_CONFIG?.SERVER_URL || 'Not configured');
      
      // Always call RGS startGame API (use funMode=1 if no token provided)
      console.info('[GameApp] Calling RGS /start endpoint...');
      const startResponse = await this.startGame();
      
      console.info('[GameApp] ✅ RGS connection successful');
      this.onStartResponse(startResponse);
      this.setState(GameState.IDLE);
      eventBus.emit(GameEvent.APP_READY, {});
      console.info('[GameApp] ✅ Game initialized successfully');
    } catch (err: any) {
      console.error('[GameApp] ❌ Fatal error during initialization:', {
        error: err.message,
        stack: err.stack,
        rgsUrl: window.config?.SERVER_CONFIG?.SERVER_URL
      });
      this.handleFatalError(err);
    }
  }

  protected onStateChanged(prev: GameState, next: GameState): void {
    console.info(`Game state changed from ${prev} to ${next}`);
  }

  private async startGame(): Promise<StartResponse> {
    // Use URL parameters if available, otherwise use defaults
    // If no token provided, use funMode=1 for demo/fun mode
    const funMode = this.urlParams.token ? 
      (this.urlParams.funMode ? parseInt(this.urlParams.funMode, 10) : 0) : 
      1;
    
    return rgsClient.startGame({
      languageId: this.urlParams.languageId || 'en',
      client: this.urlParams.client || 'desktop',
      funMode: funMode,
      token: this.urlParams.token || ''
    });
  }

  async requestSpin(bet: number): Promise<void> {
    if (this.state !== GameState.IDLE && this.state !== GameState.RESPIN) return;
    this.setState(GameState.SPINNING);
    eventBus.emit(GameEvent.SPIN_STARTED, {});

    // Use executeSpin which now handles RGS calls
    await this.executeSpin();
    
    // End of spin
    this.isSpinning = false;
    this.setState(GameState.IDLE);
    eventBus.emit(GameEvent.SPIN_RESULT, {});
  }

  private onStartResponse(response: StartResponse): void {
    console.info('Start response received', response);
    
    // Extract session ID and balance from RGS response
    if (response.player) {
      this.sessionId = response.player.sessionId;
      this.balance = response.player.balance;
      this.bottomBar.setBalance(this.balance);
    }
    
    // Update currency display if needed
    if (response.currency) {
      // Currency formatting can be handled here if needed
    }
    
    // Handle game configuration from response
    if (response.game) {
      // Update bet levels if provided
      if (response.game.bet && response.game.bet.levels) {
        // Can update bet selector with these levels
      }
    }
  }

  private async onSpinResult(response: PlayResponse): Promise<void> {
    console.info('Spin result received', response);
    console.info('Game results structure:', response.game?.results);
    
    // Update balance from RGS response
    if (response.player) {
      this.balance = response.player.balance;
      this.bottomBar.setBalance(this.balance);
    }
    
    // Handle round ID and transaction IDs if needed for history/replay
    if (response.player?.roundId) {
      // Store round ID for potential replay functionality
    }

    // Extract reel symbols from engine results and process the spin
    if (response.game?.results) {
      console.info('[GameApp] ✅ Game engine results received');
      const { convertEngineResultsToGrid } = await import('@game/utils/engineResultConverter');
      const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
      
      // Handle nested structure: RGS wraps ResultsEnvelope in an envelope
      // Check if results is wrapped: { statusCode, message, results: { finalGridSymbols, ... } }
      let actualResults = response.game.results;
      if (actualResults && typeof actualResults === 'object') {
        const results = actualResults as any;
        if (results.results && typeof results.results === 'object' && results.statusCode !== undefined) {
          console.info('[GameApp] Detected nested results structure, unwrapping...');
          actualResults = results.results;
        }
      }
      
      // Log the results structure for debugging
      console.info('[GameApp] Converting engine results to grid:', {
        resultsType: typeof actualResults,
        resultsKeys: actualResults && typeof actualResults === 'object' ? Object.keys(actualResults) : 'N/A',
        resultsPreview: actualResults && typeof actualResults === 'object' ? 
          JSON.stringify(actualResults).substring(0, 200) : 'N/A',
        expectedGridSize: `${layout.cols}x${layout.rows} = ${layout.cols * layout.rows}`
      });
      
      const grid = convertEngineResultsToGrid(actualResults, layout.cols, layout.rows);
      
      // Extract wins from backend response
      const { extractWinsFromEngineResults } = await import('@game/utils/engineResultConverter');
      const backendWins = extractWinsFromEngineResults(actualResults, layout.cols, layout.rows);
      const backendTotalWin = response.player?.win ?? 0;
      
      if (grid) {
        console.info('[GameApp] ✅ Grid conversion successful:', {
          gridSize: `${grid.length}x${grid[0]?.length || 0}`,
          totalSymbols: grid.flat().length,
          backendWinsCount: backendWins.length,
          backendTotalWin
        });
        // Process the spin result with the actual engine data and backend wins
        await this.processSpinResult({ symbols: grid }, backendWins, backendTotalWin);
      } else {
        console.error('[GameApp] ❌ Game Engine Error: Could not extract grid from engine results', {
          results: response.game.results,
          resultsType: typeof response.game.results,
          cols: layout.cols,
          rows: layout.rows,
          expectedSize: layout.cols * layout.rows
        });
        throw new Error('Game Engine Error: Invalid results format. Check Game Engine logs.');
      }
    } else {
      console.error('[GameApp] ❌ Game Engine Error: No engine results in RGS response', { 
        game: response.game,
        hasGame: !!response.game,
        hasResults: !!response.game?.results
      });
      throw new Error('Game Engine Error: Missing results in response. Check Game Engine service.');
    }
  }

  private handleNetworkError(error: unknown): void {
    const errorMessage = error instanceof Error ? error.message : String(error);
    const errorDetails = error instanceof Error ? {
      message: error.message,
      stack: error.stack,
      name: error.name
    } : { error };
    
    console.error('[GameApp] ❌ Network error:', errorDetails);
    console.error('[GameApp] Current RGS URL:', window.config?.SERVER_CONFIG?.SERVER_URL);
    
    // Determine error type and provide specific messages
    let userMessage = 'Network error occurred';
    if (errorMessage.includes('RGS') || errorMessage.includes('connect')) {
      userMessage = 'Cannot connect to game server. Please check your connection.';
    } else if (errorMessage.includes('Game Engine') || errorMessage.includes('engine results')) {
      userMessage = 'Game engine error. Please try again.';
    } else if (errorMessage.includes('401') || errorMessage.includes('Authentication')) {
      userMessage = 'Session expired. Please refresh the page.';
    } else if (errorMessage.includes('500') || errorMessage.includes('Internal Server Error')) {
      userMessage = 'Server error. Please try again later.';
    }
    
    eventBus.emit(GameEvent.NETWORK_ERROR, { message: userMessage });
    this.setState(GameState.ERROR);
  }

  private handleFatalError(error: unknown): void {
    console.error('Fatal error', error);
    eventBus.emit(GameEvent.FATAL_ERROR, { message: 'Fatal error occurred' });
    this.setState(GameState.ERROR);
  }

  private async handleSpinButtonClick(): Promise<void> {
    // If win animations are in progress, cancel them and proceed with spin
    if (this.isWinAnimating || this.paylineView.getIsAnimating()) {
      console.info('[GameApp] Win animations in progress - cancelling and starting spin...');
      // Cancel win animations
      this.reelsView.clearWins();
      this.paylineIndicators.clearHighlights();
      this.paylineView.clearPaylines();
      this.winPanel.hideWin();
      this.bottomBar.setWin(0);
      this.isWinAnimating = false; // Reset flag
      // Continue to start spin below
    }
    
    // Ignore if something is already in progress (spinning)
    if (this.isSpinning) {
      // If spinning and not in feature, allow fast-stop
      if (!this.inRespinFeature) {
        this.reelsView.requestFastStop();
        this.audioManager.stop('spin');
        this.audioManager.play('stop');
      }
      return;
    }

    // Clear any remaining win animations before starting new spin
    // (Already cleared above if animations were in progress, but ensure clean state)
    if (!this.isWinAnimating && !this.paylineView.getIsAnimating()) {
      this.reelsView.clearWins();
      this.paylineIndicators.clearHighlights();
      this.paylineView.clearPaylines();
      this.winPanel.hideWin();
      this.bottomBar.setWin(0);
    }

    // Top-level round orchestration (base spin + auto respins)
    this.isSpinning = true;
    this.bottomBar.setSpinEnabled(false);
    this.bottomBar.setSpinButtonText('SPIN');  // base spin

    // Clear previous state
    this.stickyWildReels.clear();
    this.inRespinFeature = false;
    this.pendingRespins = 0;
    this.reelsView.clearLockedReels();

    // Stop auto spin if active
      if (this.isAutoSpinning) {
        this.isAutoSpinning = false;
        this.autoSpinCount = 0;
      }
      
    try {
      // Base spin (not a respin)
      console.info('[GameApp] Starting base spin...');
      const baseResult = await this.playRound(false);
      console.info('[GameApp] Base spin completed. inRespinFeature:', this.inRespinFeature, 'pendingRespins:', this.pendingRespins);

      // Check if respins were triggered
      if (this.inRespinFeature && this.pendingRespins > 0) {
        console.info('[GameApp] Respins triggered! Starting respin loop...');
        // Show RESPIN label while feature is active (button stays disabled)
        this.bottomBar.setSpinButtonText('RESPIN');

        // Automatically play respins until feature ends
        while (this.pendingRespins > 0 && this.inRespinFeature) {
          console.info(`[GameApp] Starting respin. Remaining: ${this.pendingRespins}`);
          // Decrement BEFORE the respin (we're about to use one)
          this.pendingRespins--;
          // Ensure isSpinning is true for respin animation
          this.isSpinning = true;
          console.info('[GameApp] isSpinning set to true for respin');
          
          // Clear all win animations before starting next respin
          // This happens after the previous respin's win animations have completed
          console.info('[GameApp] Clearing win animations before respin...');
          this.reelsView.clearWins();
          this.paylineIndicators.clearHighlights();
          this.paylineView.clearPaylines();
          this.winPanel.hideWin();
          this.bottomBar.setWin(0);
          
          // Win display is already handled in onSpinAnimationComplete (it waits for win display)
          // No need for extra delays - the sequencing is now handled by awaiting onSpinAnimationComplete
          console.info('[GameApp] Starting respin (win display should have completed)...');
          
          const respinResult = await this.playRound(true);
          console.info('[GameApp] Respin completed. inRespinFeature:', this.inRespinFeature, 'pendingRespins:', this.pendingRespins, 'isSpinning:', this.isSpinning);
          
          // Check if feature should continue
          if (!this.inRespinFeature || this.pendingRespins <= 0) {
            console.info('[GameApp] Respin feature ended, breaking loop');
            break;
          }
        }
        console.info('[GameApp] Respin loop finished');
      } else {
        console.info('[GameApp] No respins triggered');
      }
    } catch (err) {
      console.error('[GameApp] Error in spin flow:', err);
      this.handleNetworkError(err);
    } finally {
      // Reset state after base + all respins are completely finished
      // IMPORTANT: This always runs, even if there are wins displaying
      console.info('[GameApp] Resetting spin state in finally block');
      console.info('[GameApp] State before reset:', {
        inRespinFeature: this.inRespinFeature,
        pendingRespins: this.pendingRespins,
        isSpinning: this.isSpinning
      });
      
      this.inRespinFeature = false;
      this.pendingRespins = 0;
      this.isSpinning = false;

      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);

      console.info('[GameApp] Button should now be enabled. isSpinning:', this.isSpinning);
    }
  }

  private async executeSpin(): Promise<void> {
    this.isSpinning = true;
    // Change button text to STOP and keep it enabled so it can be clicked
    this.bottomBar.setSpinButtonText('STOP');
    this.bottomBar.setSpinEnabled(true);

    // Get bet amount
    const betAmount = this.bottomBar.getCurrentBet();

    // Clear previous win highlights before a new spin
    // Note: Spin sound is played in playRound() when the spin actually starts
    this.reelsView.clearWins();
    this.paylineIndicators.clearHighlights();
    this.paylineView.clearPaylines();
    this.winPanel.hideWin();

    // Reset respin / wild state for a new player-initiated spin
        this.stickyWildReels.clear();
        this.inRespinFeature = false;
        this.pendingRespins = 0;
    this.reelsView.clearLockedReels();

    try {
      // Always call RGS play endpoint
      if (!this.sessionId) {
        throw new Error('No session ID available. Game must be initialized with RGS.');
      }

      console.info('[GameApp] Executing spin with RGS...');
      // Round bet amount to 2 decimal places to match backend decimal(20,2) format
      const { roundToTwoDecimals } = await import('@game/utils/betUtils');
      const roundedBet = roundToTwoDecimals(betAmount);
      const result: PlayResponse = await rgsClient.play({
        sessionId: this.sessionId,
        baseBet: roundedBet,
        betMode: 'standard', // Default bet mode
        bets: [{ betType: 'BASE', amount: roundedBet }]
      });

      console.info('[GameApp] ✅ Spin response received from RGS');
      
      // Update balance from RGS response (backend handles deduction)
      if (result.player) {
        this.balance = result.player.balance;
        this.bottomBar.setBalance(this.balance);
        console.info('[GameApp] Balance updated:', this.balance);
      }

      // Process the spin result
      console.info('[GameApp] Processing spin result...');
      await this.onSpinResult(result);
      console.info('[GameApp] ✅ Spin processing complete');
      
      // If we're in a respin feature, the button will be managed by playSpinCycle/endRespinFeature
      // Don't reset state here if respin is active
      if (!this.inRespinFeature) {
        this.isSpinning = false;
        this.bottomBar.setSpinButtonText('SPIN');
        this.bottomBar.setSpinEnabled(true);
      }
    } catch (err) {
      // On error, always end respin feature and reset state
      if (this.inRespinFeature) {
        this.endRespinFeature();
      }
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.handleNetworkError(err);
    } finally {
      // Only reset state if not in respin and not auto spinning
      if (!this.inRespinFeature && !this.isAutoSpinning) {
        // State already reset above, but ensure button is enabled
        this.bottomBar.setSpinEnabled(true);
      }
      
      // Continue auto spin if active (but not during respin)
      if (this.isAutoSpinning && this.autoSpinCount > 0 && !this.inRespinFeature && !this.isSpinning) {
        this.autoSpinCount--;
        // Small delay before next auto spin
        await new Promise<void>((resolve) => setTimeout(resolve, 1000));
        
        if (this.autoSpinCount > 0 && this.isAutoSpinning) {
          // Continue auto spinning
          this.bottomBar.setSpinButtonText('STOP');
          this.bottomBar.setSpinEnabled(true);
          await this.executeSpin();
        } else {
          // Auto spin finished
          this.stopAutoSpin();
        }
      }
    }
  }

  private startAutoSpin(count: number): void {
    this.isAutoSpinning = true;
    this.autoSpinCount = count;
    // Update button to show stop state
    this.bottomBar.setSpinEnabled(true);
    // Start first spin
    void this.executeSpin();
  }

  private stopAutoSpin(): void {
    this.isAutoSpinning = false;
    this.autoSpinCount = 0;
    this.bottomBar.setSpinButtonText('SPIN');
    this.bottomBar.setSpinEnabled(true);
  }

  private async handleFeatureButtonClick(): Promise<void> {
    // Feature button functionality should be handled by RGS/backend
    // Feature buy should call RGS /buy-free-spins endpoint
    // For now, this is a placeholder - feature buy not yet implemented
    console.warn('Feature button clicked - feature buy not yet implemented with RGS');
  }

  /**
   * Centralized spin completion handler.
   * Called ONCE when reels have fully stopped (natural end or fast-stop).
   * This processes wild expansion, win evaluation, and determines if respins should continue.
   * Returns true if respins should continue, false otherwise.
   */
  private async onSpinAnimationComplete(): Promise<boolean> {
    // This must only run when spinning
    if (!this.isSpinning) {
      console.warn('[GameApp] onSpinAnimationComplete called but isSpinning is false');
      return false;
    }

    console.info('[GameApp] Spin animation complete, processing wilds and wins...');

    // Read the final grid now shown on reels
    let grid = this.reelsView.getCurrentGrid();

    // Detect new wild reels from the INITIAL grid (before wild expansion)
    // Use the stored initial grid from playRound, or fallback to current grid
    const initialGrid = this.currentInitialGrid || grid;
    
    // Detect new wild reels from INITIAL grid (before expansion)
    const newWildReels = this.detectNewWildReels(initialGrid);
    console.info('[GameApp] Detected new wild reels from initial grid:', newWildReels);

    // Expand new wilds and mark as sticky
    if (newWildReels.length > 0) {
      console.info('[GameApp] Expanding wild reels:', newWildReels);
      for (const col of newWildReels) {
        // Expand visually
        await this.reelsView.expandWildReels([col]);
        this.reelsView.lockReels([col]);
        // Mark logic as sticky for future respins
        this.stickyWildReels.add(col);
      }
      // Refresh grid after expansion (so all wild rows are SYM_WILD)
      grid = this.reelsView.getCurrentGrid();
    }

    // Evaluate and pay wins for this spin (with expanded wilds)
    // Wins come from backend - no local calculation
    // For BOTH base spins and respins: always wait for win animations to finish
    try {
      await this.evaluateAndApplyWins(grid);
      console.info('[GameApp] Win display completed (base or respin), safe to continue');
    } catch (err) {
      console.error('[GameApp] Error displaying wins:', err);
    }

    // Decide if we must start or continue the respin feature
    const totalSticky = this.stickyWildReels.size;
    const justStartedFeature = !this.inRespinFeature && totalSticky > 0;

    if (justStartedFeature) {
      this.inRespinFeature = true;
      this.pendingRespins = 1; // at least 1 respin when feature starts
      console.info('[GameApp] Respin feature triggered, sticky reels:', Array.from(this.stickyWildReels));
    }

    // If we are in feature and got new wilds, may add extra respins (max 3 sticky reels)
    if (this.inRespinFeature && newWildReels.length > 0) {
      const freeSlots = 3 - totalSticky + newWildReels.length;
      if (freeSlots > 0) {
        const currentStickyBeforeNew = totalSticky - newWildReels.length;
        this.pendingRespins = Math.min(3 - currentStickyBeforeNew, this.pendingRespins + 1);
        console.info('[GameApp] Additional respin granted, sticky reels:', Array.from(this.stickyWildReels));
      }
    }

    // IMPORTANT: clamp sticky reels count to max 3
    if (this.stickyWildReels.size > 3) {
      const trimmed = Array.from(this.stickyWildReels).slice(0, 3);
      this.stickyWildReels = new Set(trimmed);
      console.warn('[GameApp] Trimmed sticky reels to max 3:', Array.from(this.stickyWildReels));
    }

    // Return whether respins should continue (DON'T decrement here - let handleSpinButtonClick do it)
    // IMPORTANT: Don't end the feature here if we're in the middle of a respin loop
    // The handleSpinButtonClick loop will handle ending the feature
    const shouldContinue = this.inRespinFeature && this.pendingRespins > 0 && this.stickyWildReels.size > 0;
    if (shouldContinue) {
      console.info('[GameApp] Respin should continue, remaining:', this.pendingRespins);
    } else if (this.inRespinFeature && this.pendingRespins <= 0) {
      // Only end feature if we're sure it's done (no more respins)
      // But don't clear locked reels yet - let the finally block do it
      console.info('[GameApp] Feature ended (no more respins)');
      this.inRespinFeature = false;
      this.pendingRespins = 0;
      // Don't clear stickyWildReels or lockedReels here - let handleSpinButtonClick finally block do it
    }

    return shouldContinue;
  }

  /**
   * Play one round (base spin or respin).
   * Centralizes: calling RGS, animating reels, applying wilds, updating HUD.
   * Returns the PlayResponse and waits for animation completion.
   */
  private async playRound(isRespin: boolean): Promise<PlayResponse> {
    console.info(`[GameApp] Playing ${isRespin ? 'respin' : 'base'} round...`);

    // Clear all win animations immediately before starting spin/respin
    // This ensures no win displays are visible while reels are spinning
    this.reelsView.clearWins();
    this.paylineIndicators.clearHighlights();
    this.paylineView.clearPaylines();
    this.winPanel.hideWin();
    this.bottomBar.setWin(0);

    // Small delay before respin for better UX (gives time to see expanded wilds)
    if (isRespin) {
      await new Promise<void>((resolve) => setTimeout(resolve, 800)); // Increased from 500ms to 800ms
    }

    // Get bet amounts (line bet and total bet)
    const lineBet = this.bottomBar.getCurrentLineBet();
    const totalBet = this.bottomBar.getCurrentTotalBet();

    // Call RGS
    if (!this.sessionId) {
      throw new Error('No session ID available. Game must be initialized with RGS.');
    }

    console.info(`[GameApp] Calling RGS for ${isRespin ? 'respin' : 'base spin'}...`);
    const response: PlayResponse = await rgsClient.play({
      sessionId: this.sessionId,
      baseBet: totalBet, // Total bet sent to RGS
      betMode: 'standard',
      bets: [{ betType: 'BASE', amount: totalBet }]
    });

    // Update balance
    if (response.player) {
      this.balance = response.player.balance;
      this.bottomBar.setBalance(this.balance);
    }

    // Extract grid and wins from RGS response
    if (!response.game?.results) {
      throw new Error('No results in RGS response');
    }

    const { convertEngineResultsToGrid, extractWinsFromEngineResults } = await import('@game/utils/engineResultConverter');
    const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
    
    // Handle nested structure: response.game.results contains the ResultsEnvelope
    // Structure: { game: { results: { finalGridSymbols: [...], wins: [...], cascades: [...] } } }
    let actualResults = response.game?.results;
    
    console.info('[GameApp] Raw response structure:', {
      hasGame: !!response.game,
      hasResults: !!response.game?.results,
      resultsKeys: response.game?.results ? Object.keys(response.game.results as any) : [],
      resultsType: typeof response.game?.results,
      resultsSample: response.game?.results ? JSON.stringify(response.game.results).substring(0, 200) : 'none'
    });
    
    if (!actualResults || typeof actualResults !== 'object') {
      throw new Error('No results found in response.game.results');
    }
    
    const resultGrid = convertEngineResultsToGrid(actualResults, layout.cols, layout.rows);
    
    if (!resultGrid) {
      console.error('[GameApp] Failed to convert grid. Results structure:', actualResults);
      throw new Error('Failed to convert engine results to grid');
    }
    
    console.info('[GameApp] Grid converted successfully:', {
      cols: resultGrid.length,
      rows: resultGrid[0]?.length,
      sample: resultGrid.map(col => col[0]).slice(0, 3)
    });
    
    // Extract wins from backend response (NO local calculation)
    const backendWins = extractWinsFromEngineResults(actualResults, layout.cols, layout.rows);
    const backendTotalWin = response.player?.win ?? 0;
    
    // Store backend wins for use in evaluateAndApplyWins
    this.currentBackendWins = backendWins;
    this.currentBackendTotalWin = backendTotalWin;
    
    console.info('[GameApp] Backend wins extracted:', {
      winsCount: backendWins.length,
      totalWin: backendTotalWin,
      wins: backendWins.map(w => `Line ${w.lineId}: ${w.symbol} x${w.count} = ${w.payout.toFixed(2)}`)
    });

    if (!resultGrid) {
      throw new Error('Failed to convert engine results to grid');
    }

    // Get initial grid (before wild expansion) from first cascade if available
    // This is needed to detect NEW wilds before they're expanded
    let initialGridForWildDetection = resultGrid;
    if (actualResults && typeof actualResults === 'object') {
      const results = actualResults as any;
      if (results.cascades && Array.isArray(results.cascades) && results.cascades.length > 0) {
        const firstCascade = results.cascades[0];
        if (firstCascade.gridBefore && Array.isArray(firstCascade.gridBefore)) {
          // Convert initial grid from backend format
          const initialGridConverted = convertEngineResultsToGrid(
            { finalGridSymbols: firstCascade.gridBefore },
            layout.cols,
            layout.rows
          );
          if (initialGridConverted) {
            initialGridForWildDetection = initialGridConverted;
            console.info('[GameApp] Using initial grid from first cascade for wild detection');
          }
        }
      }
    }

    // Apply sticky wilds for respins
    const symbolsWithSticky = isRespin 
      ? this.applyStickyWildsToResult(resultGrid)
      : resultGrid;

    // Store initial grid for wild detection (before expansion)
    this.currentInitialGrid = initialGridForWildDetection;

    // Create a promise that resolves when animation completes and processing is done
    let animationCompleteResolve: (value: PlayResponse) => void;
    let animationCompleteReject: (reason?: any) => void;
    const animationCompletePromise = new Promise<PlayResponse>((resolve, reject) => {
      animationCompleteResolve = resolve;
      animationCompleteReject = reject;
    });

    // Override the callback temporarily to capture completion
    const originalCallback = this.reelsView.onSpinAnimationComplete;
    let callbackCalled = false;
    this.reelsView.onSpinAnimationComplete = async () => {
      if (callbackCalled) {
        console.warn('[GameApp] playRound callback called multiple times, ignoring');
        return;
      }
      callbackCalled = true;

      try {
        // Process wilds and wins - this sets inRespinFeature and pendingRespins
        await this.onSpinAnimationComplete();
        
        // Resolve our promise with the response AFTER processing is complete
        animationCompleteResolve(response);
      } catch (err) {
        console.error('[GameApp] Error in playRound callback:', err);
        animationCompleteReject(err);
      } finally {
        // Restore original callback AFTER resolving
        this.reelsView.onSpinAnimationComplete = originalCallback || (async () => {});
      }
    };

    // Ensure isSpinning is true before animation (required for callback to process)
    if (!this.isSpinning) {
      console.warn('[GameApp] playRound: isSpinning was false, setting to true');
      this.isSpinning = true;
    }

    console.info(`[GameApp] playRound: About to animate ${isRespin ? 'respin' : 'base spin'}. Locked reels:`, Array.from(this.reelsView['lockedReels'] || new Set()));

    // Play spin sound RIGHT BEFORE animation starts (not before RGS call)
    // This ensures the sound plays when the reels actually start spinning
    this.audioManager.play('spin');

    // Animate reels - callback will be called when done
    // Note: Spin sound is stopped in ReelsView.animateSpinTo() when reels stop (before win animations)
    await this.reelsView.animateSpinTo(symbolsWithSticky, this.spinSpeed);

    // Wait for animation completion AND wild/win processing to complete
    // This ensures pendingRespins is set before we check it in handleSpinButtonClick
    return animationCompletePromise;
  }

  /**
   * @deprecated Use playRound instead
   * Play a base spin (triggered by player clicking SPIN button).
   */
  private async playBaseSpin(): Promise<void> {
    console.info('[GameApp] Starting base spin...');

    // Get bet amount
    const betAmount = this.bottomBar.getCurrentBet();

    // Play spin sound
    this.audioManager.play('spin');

    try {
      // Call RGS for base spin
      if (!this.sessionId) {
        throw new Error('No session ID available. Game must be initialized with RGS.');
      }

      console.info('[GameApp] Calling RGS for base spin...');
      const result: PlayResponse = await rgsClient.play({
        sessionId: this.sessionId,
        baseBet: betAmount,
        betMode: 'standard',
        bets: [{ betType: 'BASE', amount: betAmount }]
      });

      // Update balance
      if (result.player) {
        this.balance = result.player.balance;
        this.bottomBar.setBalance(this.balance);
      }

      // Extract grid from RGS response
      if (!result.game?.results) {
        throw new Error('No results in RGS response');
      }

      const { convertEngineResultsToGrid } = await import('@game/utils/engineResultConverter');
      const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
      const baseResultGrid = convertEngineResultsToGrid(result.game.results, layout.cols, layout.rows);

      if (!baseResultGrid) {
        throw new Error('Failed to convert engine results to grid');
      }

      // For base spin, no sticky wild override applied
      // Animate reels - onSpinAnimationComplete will be called when done
      // Note: Spin sound is stopped in ReelsView.animateSpinTo() when reels stop (before win animations)
      await this.reelsView.animateSpinTo(baseResultGrid, this.spinSpeed);

      // Note: onSpinAnimationComplete will be called by ReelsView when animation finishes
    } catch (err) {
      console.error('[GameApp] Error in playBaseSpin:', err);
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);
      this.handleNetworkError(err);
      throw err;
    }
  }

  /**
   * Play a respin cycle (triggered internally during feature).
   */
  private async playRespinCycle(): Promise<void> {
    console.info('[GameApp] Starting respin cycle...');

    // Small delay before respin for better UX
    await new Promise<void>((resolve) => setTimeout(resolve, 500));

    // Get bet amount
    const betAmount = this.bottomBar.getCurrentBet();

    // Play spin sound for respin
    this.audioManager.play('spin');

    try {
      // Call RGS for respin
      if (!this.sessionId) {
        throw new Error('No session ID available. Game must be initialized with RGS.');
      }

      console.info('[GameApp] Calling RGS for respin...');
      const result: PlayResponse = await rgsClient.play({
        sessionId: this.sessionId,
        baseBet: betAmount,
        betMode: 'standard',
        bets: [{ betType: 'BASE', amount: betAmount }]
        // Note: In a full implementation, sticky wild state would be passed here
      });

      // Update balance
      if (result.player) {
        this.balance = result.player.balance;
        this.bottomBar.setBalance(this.balance);
      }

      // Extract grid from RGS response
      if (!result.game?.results) {
        throw new Error('No results in RGS response');
      }

      const { convertEngineResultsToGrid } = await import('@game/utils/engineResultConverter');
      const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
      const respinResultGrid = convertEngineResultsToGrid(result.game.results, layout.cols, layout.rows);

      if (!respinResultGrid) {
        throw new Error('Failed to convert engine results to grid');
      }

      // Apply sticky wilds (keep full wild reels)
      const symbolsWithSticky = this.applyStickyWildsToResult(respinResultGrid);

      // Spin ONLY non-sticky reels (locked reels are skipped in animateSpinTo)
      // When animation ends, ReelsView calls onSpinAnimationComplete again,
      // which will expand any new wilds, pay respin wins, and either chain another respin or end the feature.
      // Note: Spin sound is stopped in ReelsView.animateSpinTo() when reels stop (before win animations)
      await this.reelsView.animateSpinTo(symbolsWithSticky, this.spinSpeed);

      // Note: onSpinAnimationComplete will be called by ReelsView when animation finishes
    } catch (err) {
      console.error('[GameApp] Error in playRespinCycle:', err);
      // End feature on error
      this.inRespinFeature = false;
      this.pendingRespins = 0;
      this.stickyWildReels.clear();
      this.reelsView.clearLockedReels();
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);
      this.handleNetworkError(err);
      throw err;
    }
  }

  /**
   * Centralized spin/respin cycle handler.
   * Handles base spin, expanding wilds, paying wins, and looping respins until feature ends.
   * This is the single entry point for all spin logic.
   */
  private async playSpinCycle(isRespin: boolean): Promise<void> {
    console.info(`[GameApp] playSpinCycle called. isRespin: ${isRespin}, inRespinFeature: ${this.inRespinFeature}, pendingRespins: ${this.pendingRespins}`);
    
    // 1) Get the next spin result from RGS
    let rgsResponse: PlayResponse;
    try {
      const betAmount = this.bottomBar.getCurrentBet();
      
      if (isRespin) {
        // Small delay before respin for better UX
        await new Promise<void>((resolve) => setTimeout(resolve, 500));
        this.bottomBar.setSpinButtonText('RESPIN');
      }
      
      // Play spin sound
      this.audioManager.play('spin');
      
      console.info(`[GameApp] Calling RGS for ${isRespin ? 'respin' : 'base spin'}...`);
      rgsResponse = await rgsClient.play({
        sessionId: this.sessionId!,
        baseBet: betAmount,
        betMode: 'standard',
        bets: [{ betType: 'BASE', amount: betAmount }]
        // Note: In a full implementation, sticky wild state would be passed here
      });
      
      // Update balance
      if (rgsResponse.player) {
        this.balance = rgsResponse.player.balance;
        this.bottomBar.setBalance(this.balance);
      }
    } catch (err) {
      console.error('[GameApp] RGS call failed:', err);
      this.endRespinFeature();
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);
      this.handleNetworkError(err);
      return;
    }
    
    // Extract grid from RGS response
    if (!rgsResponse.game?.results) {
      console.error('[GameApp] No results in RGS response');
      this.endRespinFeature();
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);
      return;
    }
    
    const { convertEngineResultsToGrid } = await import('@game/utils/engineResultConverter');
    const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
    const baseResultGrid = convertEngineResultsToGrid(rgsResponse.game.results, layout.cols, layout.rows);
    
    if (!baseResultGrid) {
      console.error('[GameApp] Failed to convert engine results to grid');
      this.endRespinFeature();
      this.isSpinning = false;
      this.bottomBar.setSpinButtonText('SPIN');
      this.bottomBar.setSpinEnabled(true);
      return;
    }
    
    // 2) Apply sticky wild reels for respins
    const symbolsWithSticky = this.applyStickyWildsToResult(baseResultGrid);
    
    // 3) Animate the reels to this final grid
    // Note: Spin sound is stopped in ReelsView.animateSpinTo() when reels stop (before win animations)
    await this.reelsView.animateSpinTo(symbolsWithSticky, this.spinSpeed);
    
    // 4) Get the actual grid now shown on screen (symbol IDs)
    let grid = this.reelsView.getCurrentGrid();
    
    // 5) Detect new wild reels (only reels 1,2,3 in 0-based)
    const newWildReels = this.detectNewWildReels(grid);
    console.info('[GameApp] Detected new wild reels:', newWildReels);
    
    // 6) VISUAL EXPANSION of new wilds and marking them sticky
    if (newWildReels.length > 0) {
      console.info('[GameApp] Expanding wild reels:', newWildReels);
      
      // Expand visually
      await this.reelsView.expandWildReels(newWildReels);
      this.reelsView.lockReels(newWildReels);
      
      // Mark logic as sticky for future respins
      for (const col of newWildReels) {
        this.stickyWildReels.add(col);
      }
      
      // After expansion, ensure grid reflects full wild reels for win evaluation
      // Small delay to ensure grid is fully updated
      await new Promise<void>((resolve) => setTimeout(resolve, 100));
      grid = this.reelsView.getCurrentGrid();
      
      console.info('[GameApp] Grid after wild expansion in playSpinCycle:', grid.map((col, idx) => 
        `Reel ${idx + 1}: [${col.join(', ')}]`
      ).join('\n'));
    }
    
    // 7) Evaluate and pay wins for THIS spin
    // This will wait for all win animations to complete before returning
    await this.evaluateAndApplyWins(grid);
    
    // IMPORTANT: Check if win animations were cancelled (player pressed spin again)
    // If so, don't continue with respins - let the new spin handle it
    if (!this.inRespinFeature || this.pendingRespins <= 0) {
      console.info('[GameApp] Respin feature ended or cancelled, not continuing respins');
      this.endRespinFeature();
      return;
    }
    
    // 8) If we just created any new sticky wilds and we are not at max sticky reels, grant respins
    const totalSticky = this.stickyWildReels.size;
    const justTriggeredFeature = !this.inRespinFeature && totalSticky > 0;
    
    if (justTriggeredFeature) {
      this.inRespinFeature = true;
      // First respin
      this.pendingRespins = 1;
      console.info('[GameApp] Respin feature triggered, sticky reels:', Array.from(this.stickyWildReels));
    } else if (this.inRespinFeature && newWildReels.length > 0) {
      // If already in feature and we got new wilds, add more respins up to 3 sticky reels
      // Starburst logic: up to 3 sticky reels total; we can grant another respin per new wild on allowed reels
      const currentStickyBeforeNew = totalSticky - newWildReels.length;
      this.pendingRespins = Math.min(3 - currentStickyBeforeNew, this.pendingRespins + 1);
      console.info('[GameApp] Additional respin granted, sticky reels:', Array.from(this.stickyWildReels));
    }
    
    // IMPORTANT: clamp sticky reels count to max 3
    // If for some reason more than 3 got added, trim down.
    if (this.stickyWildReels.size > 3) {
      const trimmed = Array.from(this.stickyWildReels).slice(0, 3);
      this.stickyWildReels = new Set(trimmed);
      console.warn('[GameApp] Trimmed sticky reels to max 3:', Array.from(this.stickyWildReels));
    }
    
    // 9) Decide if we continue respins or end feature
    // Double-check that we're still in respin feature and have respins remaining
    // (player might have pressed spin again, cancelling animations and ending feature)
    if (this.inRespinFeature && this.pendingRespins > 0 && this.stickyWildReels.size > 0) {
      this.pendingRespins--;
      console.info('[GameApp] Triggering respin, remaining:', this.pendingRespins);
      
      // Trigger another respin (recursive call)
      await this.playSpinCycle(true);
    } else {
      // End of feature / normal spin
      console.info('[GameApp] Ending spin cycle. inRespinFeature:', this.inRespinFeature, 'pendingRespins:', this.pendingRespins);
      this.endRespinFeature();
    }
  }

  /**
   * @deprecated Use playSpinCycle instead
   * Main spin round handler - processes a single spin or respin.
   * Handles wild expansion, win evaluation, and respin triggering.
   */
  private async playSpinRound(isRespin: boolean, spinResult?: SpinResult): Promise<void> {
    // If this is a respin, we need to get a new spin result from RGS
    // For now, if spinResult is provided, use it (from RGS response)
    // In a full implementation, respins would call RGS again with sticky wild state
    
    if (!spinResult) {
      // This shouldn't happen in normal flow, but handle gracefully
      console.warn('[GameApp] playSpinRound called without spinResult');
      return;
    }

    // Apply sticky wilds to the result grid if in respin mode
    const symbols = this.applyStickyWildsToResult(spinResult.symbols);

    // Detect new wild reels from the symbols BEFORE animation
    // This ensures we detect wilds even if they're not yet in the visual grid
    console.info('[GameApp] Checking for wilds in symbols:', symbols.map((col, idx) => 
      `Reel ${idx + 1}: [${col.join(', ')}]`
    ).join('\n'));
    console.info('[GameApp] Locked reels before animation:', Array.from(this.reelsView['lockedReels'] || new Set()));
    
    const newWildReels = this.detectNewWildReels(symbols);
    console.info('[GameApp] Detected new wild reels:', newWildReels);

    // Ensure isSpinning is set for respins so animation happens
    // This is critical - animation completion callback needs correct state
    if (isRespin) {
      this.isSpinning = true;
      console.info('[GameApp] Setting isSpinning=true for respin animation');
    }

    // Animate reels to this result
    // Locked reels will be skipped in animateSpinTo, only non-locked reels will spin
    const lockedReelsArray = Array.from(this.reelsView['lockedReels'] || new Set());
    const nonLockedReels = [0, 1, 2, 3, 4].filter(i => !lockedReelsArray.includes(i));
    console.info('[GameApp] Starting animation');
    console.info('[GameApp] Locked reels (will NOT spin):', lockedReelsArray);
    console.info('[GameApp] Non-locked reels (WILL spin):', nonLockedReels);
    
    await this.reelsView.animateSpinTo(symbols, this.spinSpeed);
    console.info('[GameApp] Animation completed');
    // Note: Spin sound is stopped in ReelsView.animateSpinTo() when reels stop (before win animations)

    // Check if spin was stopped - if so, skip the rest
    // But allow respins to continue even if main spin was stopped
    // For respins, we always want to continue processing
    if (!this.isSpinning && !this.inRespinFeature) {
      const finalGrid = this.reelsView.getCurrentGrid();
      await this.evaluateAndApplyWins(finalGrid);
      this.endRespinFeature(); // Ensure state is cleaned up
      return;
    }
    
    // For respins, ensure isSpinning is correct during processing
    if (isRespin && !this.isSpinning) {
      console.warn('[GameApp] Respin detected but isSpinning is false - fixing');
      this.isSpinning = true;
    }

    // Get the final grid after animation
    let grid = this.reelsView.getCurrentGrid();

    // Expand wilds on new reels and mark them as sticky
    if (newWildReels.length > 0) {
      console.info('[GameApp] Expanding wild reels:', newWildReels);
      console.info('[GameApp] Current grid before expansion:', grid.map((col, idx) => 
        `Reel ${idx + 1}: [${col.join(', ')}]`
      ).join('\n'));
      
      // Expand visually - the grid should already have the wilds after animation
      await this.reelsView.expandWildReels(newWildReels);
      this.reelsView.lockReels(newWildReels);

      // Mark as sticky
      for (const col of newWildReels) {
        this.stickyWildReels.add(col);
      }

      // Refresh grid after expansion - ensure expansion is complete
      // Small delay to ensure grid is fully updated
      await new Promise<void>((resolve) => setTimeout(resolve, 100));
      grid = this.reelsView.getCurrentGrid();
      
      console.info('[GameApp] Grid after wild expansion:', grid.map((col, idx) => 
        `Reel ${idx + 1}: [${col.join(', ')}]`
      ).join('\n'));

      // Set respin state (handled by playRound now, but keep for compatibility)
      if (!this.inRespinFeature) {
        // First time we trigger the feature
        this.pendingRespins = 1;
        this.inRespinFeature = true;
        console.info('[GameApp] Respin feature triggered, sticky reels:', Array.from(this.stickyWildReels));
      } else {
        // Additional respin granted
        this.pendingRespins = Math.min(this.MAX_RESPINS, this.pendingRespins + 1);
        console.info('[GameApp] Additional respin granted, sticky reels:', Array.from(this.stickyWildReels));
      }
    }

    // Evaluate wins on the final grid
    await this.evaluateAndApplyWins(grid);

    // Decide whether to continue respins
    // If we're in respin mode and have respins remaining, trigger the respin
    // (We trigger respins even if no new wilds appeared - new wilds grant additional respins)
    if (this.inRespinFeature && this.pendingRespins > 0) {
      // Check if we've reached max sticky reels (3)
      if (this.stickyWildReels.size >= 3) {
        console.info('[GameApp] Max sticky wild reels reached (3), ending respin feature');
        this.endRespinFeature();
        return;
      }

      // Trigger next respin
      this.pendingRespins--;
      console.info('[GameApp] Triggering respin, remaining:', this.pendingRespins);
      console.info('[GameApp] New wilds detected:', newWildReels.length > 0 ? newWildReels : 'none');
      
      // Set isSpinning for the respin (so button stays disabled)
      this.isSpinning = true;
      
      // For respins, we need to call RGS again
      // Note: In a full implementation, RGS would handle respins through cascades
      // For now, we'll trigger a new spin request
      await this.executeRespin();
      
      // After executeRespin completes, playSpinRound should have handled ending
      // But add a safety check to ensure feature ends if still active
      if (this.inRespinFeature && this.pendingRespins === 0) {
        console.info('[GameApp] Safety check: Respins exhausted, ending feature');
        this.endRespinFeature();
      }
    } else {
      // Feature is done - respins exhausted or not in respin mode
      // Always end the feature if we're in respin mode
      if (this.inRespinFeature) {
        const reason = this.pendingRespins === 0 
          ? 'respins exhausted' 
          : 'not in respin mode';
        console.info(`[GameApp] Ending respin feature - ${reason}`);
        this.endRespinFeature();
      }
    }
  }

  /**
   * Process wild expansion after a spin is stopped early.
   * Checks for wilds in the final grid and expands them if found.
   */
  private async processWildExpansionAfterStop(finalGrid: SymbolId[][]): Promise<void> {
    console.info('[GameApp] Processing wild expansion after stop');
    
    // Detect wild reels from the final grid
    const newWildReels = this.detectNewWildReels(finalGrid);
    console.info('[GameApp] Detected wild reels after stop:', newWildReels);
    
    if (newWildReels.length > 0) {
      // Expand wilds visually
      await this.reelsView.expandWildReels(newWildReels);
      this.reelsView.lockReels(newWildReels);
      
      // Mark as sticky
      for (const col of newWildReels) {
        this.stickyWildReels.add(col);
      }
      
      // Set respin state if not already in respin
      if (!this.inRespinFeature) {
        this.pendingRespins = 1;
        this.inRespinFeature = true;
        console.info('[GameApp] Respin feature triggered after stop, sticky reels:', Array.from(this.stickyWildReels));
      } else {
        // Additional respin granted
        this.pendingRespins = Math.min(this.MAX_RESPINS, this.pendingRespins + 1);
        console.info('[GameApp] Additional respin granted after stop, sticky reels:', Array.from(this.stickyWildReels));
      }
      
      // Evaluate wins on the final grid with expanded wilds
      // This will wait for all win animations to complete before returning
      const updatedGrid = this.reelsView.getCurrentGrid();
      await this.evaluateAndApplyWins(updatedGrid);
      
      // IMPORTANT: Check if win animations were cancelled (player pressed spin again)
      // If so, don't continue with respins - let the new spin handle it
      if (!this.inRespinFeature || this.pendingRespins <= 0) {
        console.info('[GameApp] Respin feature ended or cancelled after stop, not continuing respins');
        return;
      }
      
      // If we have respins remaining, trigger the respin
      if (this.inRespinFeature && this.pendingRespins > 0) {
        // Check if we've reached max sticky reels (3)
        if (this.stickyWildReels.size >= 3) {
          console.info('[GameApp] Max sticky wild reels reached (3), ending respin feature');
          this.endRespinFeature();
          return;
        }
        
        // Trigger respin
        this.pendingRespins--;
        console.info('[GameApp] Triggering respin after stop, remaining:', this.pendingRespins);
        this.isSpinning = true;
        await this.executeRespin();
      }
    } else {
      // No wilds found, just evaluate wins normally
    await this.evaluateAndApplyWins(finalGrid);
    }
  }

  /**
   * Apply sticky wild reels to a spin result grid.
   * If in respin mode, replaces symbols on sticky reels with SYM_WILD.
   */
  private applyStickyWildsToResult(resultGrid: SymbolId[][]): SymbolId[][] {
    if (!this.inRespinFeature || this.stickyWildReels.size === 0) {
      return resultGrid;
    }

    // Clone the grid
    const cloned: SymbolId[][] = resultGrid.map(col => [...col]);

    // Replace all symbols on sticky reels with SYM_WILD
    for (const col of this.stickyWildReels) {
      if (col >= 0 && col < cloned.length) {
        for (let row = 0; row < cloned[col].length; row++) {
          cloned[col][row] = 'SYM_WILD';
        }
      }
    }

    return cloned;
  }

  /**
   * Detect new wild reels on allowed reels (2-4, indices 1-3).
   * Only detects reels that aren't already sticky.
   */
  private detectNewWildReels(grid: SymbolId[][]): number[] {
    const newWildReels: number[] = [];

    // Allowed reels for wilds are 1,2,3 in 0-based (reels 2-4 visually)
    for (let col = 1; col <= 3 && col < grid.length; col++) {
      // Skip reels that are already sticky
      if (this.stickyWildReels.has(col)) continue;

      const colSymbols = grid[col];
      if (!colSymbols) continue;

      // Check if this column has a wild
      const hasWild = colSymbols.some(id => id === 'SYM_WILD');

      if (hasWild) {
        newWildReels.push(col);
      }
    }

    // Do not exceed 3 total sticky reels
    const freeSlots = 3 - this.stickyWildReels.size;
    if (newWildReels.length > freeSlots) {
      return newWildReels.slice(0, freeSlots);
    }

    return newWildReels;
  }

  /**
   * Execute a respin by calling RGS again.
   * Sticky wild reels should be preserved in the request (handled by backend).
   */
  private async executeRespin(): Promise<void> {
    if (!this.sessionId) {
      console.error('[GameApp] Cannot execute respin: no session ID');
      this.endRespinFeature();
      return;
    }

    // Ensure spin button is disabled during respin
    this.bottomBar.setSpinEnabled(false);
    this.bottomBar.setSpinButtonText('RESPIN');

    // CRITICAL: Set isSpinning BEFORE starting respin
    // This ensures the animation will happen
    this.isSpinning = true;
    console.info('[GameApp] Starting respin, isSpinning set to true');

    // Small delay before respin for better UX
    await new Promise<void>((resolve) => setTimeout(resolve, 500));

    // Play spin sound for respin
    this.audioManager.play('spin');

    try {
      const betAmount = this.bottomBar.getCurrentBet();
      
      console.info('[GameApp] Executing respin with RGS...');
      const result: PlayResponse = await rgsClient.play({
        sessionId: this.sessionId,
        baseBet: betAmount,
        betMode: 'standard',
        bets: [{ betType: 'BASE', amount: betAmount }]
        // Note: In a full implementation, sticky wild state would be passed here
        // For now, we'll apply sticky wilds on the frontend
      });

      // Update balance
      if (result.player) {
        this.balance = result.player.balance;
        this.bottomBar.setBalance(this.balance);
      }

      // Process the respin result
      if (result.game?.results) {
        const { convertEngineResultsToGrid, extractWinsFromEngineResults } = await import('@game/utils/engineResultConverter');
        const layout = computeReelLayout(this.app.renderer.width, this.app.renderer.height);
        
        // Handle nested structure if present
        let actualResults = result.game.results;
        if (actualResults && typeof actualResults === 'object') {
          const results = actualResults as any;
          if (results.results && typeof results.results === 'object' && results.statusCode !== undefined) {
            actualResults = results.results;
          }
        }
        
        const grid = convertEngineResultsToGrid(actualResults, layout.cols, layout.rows);
        
        // Extract wins from backend response (NO local calculation)
        const backendWins = extractWinsFromEngineResults(actualResults, layout.cols, layout.rows);
        const backendTotalWin = result.player?.win ?? 0;
        
        // Store backend wins for use in evaluateAndApplyWins
        this.currentBackendWins = backendWins;
        this.currentBackendTotalWin = backendTotalWin;

        if (grid) {
          await this.playSpinRound(true, { symbols: grid });
          // Safety check: ensure feature ends if playSpinRound didn't end it
          if (this.inRespinFeature) {
            console.info('[GameApp] Safety check: Feature still active after playSpinRound, ending it');
            this.endRespinFeature();
          }
        } else {
          console.error('[GameApp] Failed to convert respin results');
          this.endRespinFeature();
        }
      } else {
        console.error('[GameApp] No results in respin response');
        this.endRespinFeature();
      }
    } catch (err) {
      console.error('[GameApp] Respin error:', err);
      // Always end respin feature on error to restore button state
      this.endRespinFeature();
      // Handle error but avoid duplicate error handling
      if (!(err instanceof Error && err.message.includes('session'))) {
        this.handleNetworkError(err);
      }
    }
  }

  /**
   * End the respin feature and clean up state.
   */
  private endRespinFeature(): void {
    console.info('[GameApp] Ending respin feature');
    console.info('[GameApp] State before cleanup:', {
      inRespinFeature: this.inRespinFeature,
      pendingRespins: this.pendingRespins,
      isSpinning: this.isSpinning,
      stickyWildReels: Array.from(this.stickyWildReels)
    });
    
    this.inRespinFeature = false;
    this.pendingRespins = 0;
    // Note: isSpinning is managed by the caller (handleSpinButtonClick)
    this.stickyWildReels.clear();
    this.reelsView.clearLockedReels();
    
    console.info('[GameApp] Respin feature ended');
  }

  // Process a single spin result, expanding wilds and triggering respins if needed.
  private async processSpinResult(
    result: SpinResult,
    backendWins?: Array<{
      lineId: number;
      paylineIndex: number;
      symbol: string;
      count: number;
      payout: number;
      positions: Array<{ reel: number; row: number }>;
    }>,
    backendTotalWin?: number
  ): Promise<void> {
    // Store backend wins for use in playSpinRound
    this.currentBackendWins = backendWins;
    this.currentBackendTotalWin = backendTotalWin;
    // Use the new playSpinRound function
    await this.playSpinRound(false, result);
  }

  private async evaluateAndApplyWins(grid: SymbolId[][]): Promise<void> {
    // Prevent overlapping win animations
    if (this.isWinAnimating) {
      console.warn('[GameApp] evaluateAndApplyWins called while another win animation is in progress');
      return;
    }

    this.isWinAnimating = true;

    try {
      // Use wins from backend - NO local calculation
      const wins = this.currentBackendWins || [];
      const totalWin = this.currentBackendTotalWin ?? wins.reduce((sum: number, w) => sum + w.payout, 0);
      
      console.info('[GameApp] Using backend wins (no local calculation):', wins.length, wins.map(w => 
        `Line ${w.lineId}: ${w.symbol} x${w.count} = ${w.payout.toFixed(2)}`
      ).join(', '));
      console.info('[GameApp] Total win from backend:', totalWin);
      
      // Clear backend wins after use
      this.currentBackendWins = undefined;
      this.currentBackendTotalWin = undefined;

      // Show win lines / symbol highlights and get winning line IDs
      const winningLineIds = this.reelsView.showWins(wins);

      // Highlight payline indicators for winning lines
      if (winningLineIds.length > 0) {
        this.paylineIndicators.highlightWinningLines(winningLineIds);
        // Paylines will be shown via cycleThroughPaylines in the win display logic below
      } else {
        this.paylineView.clearPaylines();
      }

      // Show win panel if there's a win
      if (totalWin > 0) {
        // Play win sound immediately when wins are detected (big-win for large wins)
        // Use a small delay to ensure sounds are ready after spin animation
        setTimeout(() => {
          try {
            if (totalWin >= 100) {
              console.info('[GameApp] Playing big-win sound');
              this.audioManager.play('big-win');
            } else {
              console.info('[GameApp] Playing win sound');
              this.audioManager.play('win');
            }
          } catch (err) {
            console.warn('[GameApp] Failed to play win sound:', err);
          }
        }, 100);
        
        // Show total win in win box immediately (Starburst style)
        this.winPanel.showWin(totalWin);
        this.bottomBar.setWin(totalWin);
        
        // Starburst style: Cycle through paylines one at a time with value popups
        // Check for cancellation before starting payline cycle
        if (this.isWinAnimating) { // Check if still animating (not cancelled)
          const isRespin = this.inRespinFeature;
          
          if (isRespin) {
            // During respins: cycle through paylines quickly
            await this.paylineView.cycleThroughPaylines(wins, undefined, this.isAutoSpinning);
            // Additional wait after cycling completes (only if not cancelled)
            if (this.isWinAnimating) {
              await new Promise<void>((resolve) => setTimeout(resolve, 500));
            }
          } else {
            // Base spin: cycle through paylines with normal timing
            await this.paylineView.cycleThroughPaylines(wins, undefined, this.isAutoSpinning);
          }
        }
      } else {
        this.winPanel.hideWin();
        this.bottomBar.setWin(0);
        this.paylineIndicators.clearHighlights();
        this.paylineView.clearPaylines();
      }

      // Update balance
      this.balance += totalWin;
      this.bottomBar.setBalance(this.balance);

      console.info('Spin wins', { wins, totalWin, balance: this.balance });
    } finally {
      this.isWinAnimating = false;
    }
  }
}

