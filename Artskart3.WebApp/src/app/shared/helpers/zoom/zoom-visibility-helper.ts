import { ZoomConfig } from './zoom-config';

/**
 * Utility class for determining visibility of map layers based on zoom level
 */
export class ZoomVisibilityHelper {
  static shouldShowCounties(zoom: number): boolean {
    return zoom < ZoomConfig.ZOOM_COUNTIES_THRESHOLD;
  }

  static shouldShowMunicipalities(zoom: number): boolean {
    return zoom > ZoomConfig.ZOOM_COUNTIES_THRESHOLD && zoom <= ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD;
  }

  static hasCrossedThreshold(oldZoom: number, newZoom: number): boolean {
    const countiesCrossed =
      (oldZoom < ZoomConfig.ZOOM_COUNTIES_THRESHOLD && newZoom >= ZoomConfig.ZOOM_COUNTIES_THRESHOLD) ||
      (oldZoom >= ZoomConfig.ZOOM_COUNTIES_THRESHOLD && newZoom < ZoomConfig.ZOOM_COUNTIES_THRESHOLD);

    const municipalitiesCrossed =
      (oldZoom <= ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD && newZoom > ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD) ||
      (oldZoom > ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD && newZoom <= ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD);

    return countiesCrossed || municipalitiesCrossed;
  }

  static getAreaTypeForZoom(zoom: number): number | null {
    if (this.shouldShowCounties(zoom)) {
      return 2;
    }
    if (this.shouldShowMunicipalities(zoom)) {
      return 1;
    }
    return null;
  }
}
