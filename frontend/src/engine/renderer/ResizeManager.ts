import type { Application } from 'pixi.js';

export class ResizeManager {
  private onResizeHandler: () => void;

  constructor(private app: Application, private designWidth = 1920, private designHeight = 1080) {
    this.onResizeHandler = () => this.handleResize();
    window.addEventListener('resize', this.onResizeHandler);
    this.handleResize();
  }

  destroy(): void {
    window.removeEventListener('resize', this.onResizeHandler);
  }

  private handleResize(): void {
    const { innerWidth, innerHeight } = window;
    const scale = Math.min(innerWidth / this.designWidth, innerHeight / this.designHeight);
    this.app.stage.scale.set(scale);
    this.app.stage.position.set(
      (innerWidth - this.designWidth * scale) * 0.5,
      (innerHeight - this.designHeight * scale) * 0.5
    );
  }
}

