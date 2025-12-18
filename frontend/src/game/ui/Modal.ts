import { Container, Graphics, Text, TextStyle } from 'pixi.js';
import gsap from 'gsap';

/**
 * Base modal component for popup screens.
 * Provides a dark overlay, centered panel, and close button.
 */
export class Modal extends Container {
  protected overlay: Graphics;
  protected panel: Graphics;
  protected titleText: Text;
  protected closeButton: Graphics;
  protected closeButtonText: Text;
  protected contentContainer: Container;
  protected scrollMask: Graphics;
  private screenWidth: number = 1920;
  private screenHeight: number = 1080;
  private scrollable: boolean = false;
  private scrollY: number = 0;
  private maxScrollY: number = 0;
  private app?: any; // Application reference for canvas access

  constructor(title: string, scrollable: boolean = false, app?: any) {
    super();
    this.app = app;

    // Create dark overlay
    this.overlay = new Graphics();
    this.overlay.beginFill(0x000000, 0.7);
    this.overlay.drawRect(0, 0, this.screenWidth, this.screenHeight);
    this.overlay.endFill();
    this.overlay.eventMode = 'static';
    this.overlay.on('pointertap', () => this.close());

    // Create panel background
    this.panel = new Graphics();
    this.panel.beginFill(0x1a1a1a, 0.95);
    this.panel.lineStyle(3, 0xff00ff, 1);
    this.panel.drawRoundedRect(0, 0, 600, 500, 15);
    this.panel.endFill();
    this.panel.eventMode = 'static';
    this.panel.on('pointertap', (e) => e.stopPropagation());

    // Create title
    const titleStyle = new TextStyle({
      fontFamily: 'Arial',
      fontSize: 32,
      fontWeight: 'bold',
      fill: '#ffffff',
      dropShadow: true,
      dropShadowColor: 0x000000,
      dropShadowBlur: 4,
      dropShadowDistance: 2
    });
    this.titleText = new Text(title, titleStyle);
    this.titleText.anchor.set(0.5, 0);
    this.titleText.x = 300;
    this.titleText.y = 30;

    // Create close button
    this.closeButton = new Graphics();
    this.closeButton.beginFill(0x3d2817);
    this.closeButton.lineStyle(2, 0xffd700);
    this.closeButton.drawRoundedRect(0, 0, 100, 40, 8);
    this.closeButton.endFill();
    this.closeButton.x = 500;
    this.closeButton.y = 20;
    this.closeButton.eventMode = 'static';
    this.closeButton.cursor = 'pointer';
    this.closeButton.on('pointertap', () => this.close());

    this.closeButtonText = new Text('Close', {
      fontFamily: 'Arial',
      fontSize: 18,
      fontWeight: 'bold',
      fill: '#ffffff'
    });
    this.closeButtonText.anchor.set(0.5);
    this.closeButtonText.x = 50;
    this.closeButtonText.y = 20;
    this.closeButton.addChild(this.closeButtonText);

    // Create content container
    this.contentContainer = new Container();
    this.contentContainer.x = 50;
    this.contentContainer.y = 100;

    // Create scroll mask if scrollable
    this.scrollable = scrollable;
    if (scrollable) {
      this.scrollMask = new Graphics();
      this.scrollMask.beginFill(0xffffff, 1);
      this.scrollMask.drawRect(0, 0, 500, 350); // Content area height (relative to panel)
      this.scrollMask.endFill();
      this.scrollMask.renderable = false;
      this.scrollMask.x = 50;
      this.scrollMask.y = 100;
      this.contentContainer.mask = this.scrollMask;
      this.panel.addChild(this.scrollMask);

      // Enable wheel scrolling using DOM events (more reliable)
      this.panel.eventMode = 'static';
      this.panel.cursor = 'default';
      
      // Use DOM wheel event for better compatibility
      const canvas = this.app?.renderer?.view as HTMLCanvasElement;
      if (canvas) {
        const wheelHandler = (e: WheelEvent) => {
          if (this.visible && this.alpha > 0.5) {
            // Check if mouse is over the panel
            const rect = canvas.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;
            const panelX = this.panel.x;
            const panelY = this.panel.y;
            
            if (mouseX >= panelX && mouseX <= panelX + 600 &&
                mouseY >= panelY + 100 && mouseY <= panelY + 450) {
              e.preventDefault();
              e.stopPropagation();
              this.handleScroll(e.deltaY);
            }
          }
        };
        
        canvas.addEventListener('wheel', wheelHandler, { passive: false });
        
        // Store handler for cleanup if needed
        (this as any)._wheelHandler = wheelHandler;
        (this as any)._canvas = canvas;
      }
    }

    // Add to container
    this.addChild(this.overlay);
    this.addChild(this.panel);
    this.panel.addChild(this.titleText);
    this.panel.addChild(this.closeButton);
    this.panel.addChild(this.contentContainer);

    // Initially hidden
    this.visible = false;
    this.alpha = 0;
  }

  public updateLayout(width: number, height: number): void {
    this.screenWidth = width;
    this.screenHeight = height;

    // Update overlay
    this.overlay.clear();
    this.overlay.beginFill(0x000000, 0.7);
    this.overlay.drawRect(0, 0, width, height);
    this.overlay.endFill();

    // Center panel (default, can be overridden by child classes)
    // Child classes like BetPopup will override this positioning
    if (!this.panel.x && !this.panel.y) {
      this.panel.x = (width - this.panel.width) / 2;
      this.panel.y = (height - this.panel.height) / 2;
    }
    
    // Update max scroll when layout changes
    if (this.scrollable) {
      this.updateMaxScroll();
    }
  }

  public show(): void {
    this.visible = true;
    // Reset scroll position
    if (this.scrollable) {
      this.scrollY = 0;
      this.contentContainer.y = 100;
      this.updateMaxScroll();
    }
    gsap.to(this, { alpha: 1, duration: 0.3, ease: 'power2.out' });
  }

  public close(): void {
    gsap.to(this, {
      alpha: 0,
      duration: 0.3,
      ease: 'power2.in',
      onComplete: () => {
        this.visible = false;
      }
    });
  }

  protected addText(content: string, x: number, y: number, style?: Partial<TextStyle>): Text {
    const defaultStyle: TextStyle = new TextStyle({
      fontFamily: 'Arial',
      fontSize: 18,
      fill: '#ffffff',
      wordWrap: true,
      wordWrapWidth: 500,
      lineHeight: 24
    });

    const textStyle = style ? new TextStyle({ ...defaultStyle, ...style }) : defaultStyle;
    const text = new Text(content, textStyle);
    text.x = x;
    text.y = y;
    this.contentContainer.addChild(text);
    
    // Update max scroll if scrollable
    if (this.scrollable) {
      this.updateMaxScroll();
    }
    
    return text;
  }

  private handleScroll(deltaY: number): void {
    if (!this.scrollable) return;
    
    const scrollSpeed = 20;
    this.scrollY += deltaY > 0 ? scrollSpeed : -scrollSpeed;
    this.scrollY = Math.max(0, Math.min(this.scrollY, this.maxScrollY));
    
    this.contentContainer.y = 100 - this.scrollY;
  }

  private updateMaxScroll(): void {
    if (!this.scrollable) return;
    
    // Calculate content height
    let maxY = 0;
    this.contentContainer.children.forEach((child) => {
      if (child instanceof Text) {
        const childMaxY = child.y + child.height;
        maxY = Math.max(maxY, childMaxY);
      }
    });
    
    const visibleHeight = 350; // Content area height
    this.maxScrollY = Math.max(0, maxY - visibleHeight);
  }
}

