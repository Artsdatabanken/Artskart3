import { ZoomConfig } from './zoom-config';

export class ZoomVisibilityHelper {
  static shouldShowCounties(zoom: number): boolean {
    return zoom < ZoomConfig.ZOOM_COUNTIES_THRESHOLD;
  }

  static shouldShowMunicipalities(zoom: number): boolean {
    if (zoom > ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD) {
      return false;
    }
    return zoom >= ZoomConfig.ZOOM_COUNTIES_THRESHOLD;
  }

  static shouldShowAreaMarkers(zoom: number): boolean {
    return zoom <= ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD;
  }

  static hasCrossedThreshold(oldZoom: number, newZoom: number): boolean {
    return this.shouldShowCounties(oldZoom) !== this.shouldShowCounties(newZoom) ||
           this.shouldShowMunicipalities(oldZoom) !== this.shouldShowMunicipalities(newZoom) ||
           this.shouldShowAreaMarkers(oldZoom) !== this.shouldShowAreaMarkers(newZoom);
  }
}
