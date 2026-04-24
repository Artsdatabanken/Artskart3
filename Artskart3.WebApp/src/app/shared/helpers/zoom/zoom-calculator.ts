import { ZoomConfig } from './zoom-config';

/**
 * Utility class for zoom level calculations and transformations
 */
export class ZoomCalculator {
  static constrainZoom(zoom: number, minZoom: number = ZoomConfig.MIN_ZOOM, maxZoom: number = ZoomConfig.MAX_ZOOM): number {
    return Math.max(minZoom, Math.min(maxZoom, zoom));
  }

  static calculateZoomDelta(deltaY: number, sensitivity: number = ZoomConfig.SCROLL_SENSITIVITY): number {
    return deltaY > 0 ? -sensitivity : sensitivity;
  }

  static interpolateZoom(startZoom: number, targetZoom: number, progress: number): number {
    return startZoom + (targetZoom - startZoom) * progress;
  }

  static isZoomDifferenceNegligible(startZoom: number, targetZoom: number): boolean {
    return Math.abs(targetZoom - startZoom) < ZoomConfig.NEGLIGIBLE_ZOOM_DIFFERENCE;
  }

  static calculateAnimationProgress(elapsedTime: number, duration: number = ZoomConfig.ZOOM_ANIMATION_DURATION): number {
    return Math.min(elapsedTime / duration, 1);
  }
}
