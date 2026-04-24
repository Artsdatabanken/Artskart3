import { ZoomConfig } from './zoom-config';
import { ZoomCalculator } from './zoom-calculator';

/**
 * Manages zoom state and operations for the map
 */
export class ZoomState {
  private currentZoom: number;
  private targetZoom: number;
  private lastZoom: number;
  private lastScrollTime: number = 0;
  private animationFrameId: number | null = null;

  constructor(initialZoom: number = ZoomConfig.DEFAULT_ZOOM_LEVEL) {
    this.currentZoom = initialZoom;
    this.targetZoom = initialZoom;
    this.lastZoom = initialZoom;
  }

  getZoom(): number {
    return this.currentZoom;
  }


  getTargetZoom(): number {
    return this.targetZoom;
  }

  getLastZoom(): number {
    return this.lastZoom;
  }

  setZoom(zoom: number): void {
    this.currentZoom = zoom;
  }

  setTargetZoom(zoom: number): void {
    this.targetZoom = zoom;
  }

  updateLastZoom(): void {
    this.lastZoom = this.currentZoom;
  }

  canProcessScroll(): boolean {
    const now = Date.now();
    if (now - this.lastScrollTime < ZoomConfig.SCROLL_THROTTLE_MS) {
      return false;
    }
    this.lastScrollTime = now;
    return true;
  }

  getAnimationFrameId(): number | null {
    return this.animationFrameId;
  }

  setAnimationFrameId(id: number | null): void {
    this.animationFrameId = id;
  }

  cancelAnimation(): void {
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  calculateScrollZoom(deltaY: number, minZoom: number = ZoomConfig.MIN_ZOOM, maxZoom: number = ZoomConfig.MAX_ZOOM): number | null {
    const delta = ZoomCalculator.calculateZoomDelta(deltaY);
    const newTarget = ZoomCalculator.constrainZoom(this.targetZoom + delta, minZoom, maxZoom);

    if (newTarget === this.targetZoom) {
      return null;
    }

    this.targetZoom = newTarget;
    return newTarget;
  }

  reset(): void {
    this.currentZoom = ZoomConfig.DEFAULT_ZOOM_LEVEL;
    this.targetZoom = ZoomConfig.DEFAULT_ZOOM_LEVEL;
    this.lastZoom = ZoomConfig.DEFAULT_ZOOM_LEVEL;
    this.lastScrollTime = 0;
    this.cancelAnimation();
  }
}
