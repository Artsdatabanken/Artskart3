import { ZoomConfig } from './zoom-config';

/**
 * Manages zoom state and operations for the map
 */
export class ZoomState {
  private targetZoom: number;

  constructor(initialZoom: number = ZoomConfig.DEFAULT_ZOOM_LEVEL) {
    this.targetZoom = initialZoom;
  }

  calculateScrollZoom(deltaY: number, minZoom: number = ZoomConfig.MIN_ZOOM, maxZoom: number = ZoomConfig.MAX_ZOOM): number | null {
    const delta = ZoomConfig.calculateZoomDelta(deltaY);
    const newTarget = ZoomConfig.constrainZoom(this.targetZoom + delta, minZoom, maxZoom);

    if (newTarget === this.targetZoom) {
      return null;
    }

    this.targetZoom = newTarget;
    return newTarget;
  }
}
