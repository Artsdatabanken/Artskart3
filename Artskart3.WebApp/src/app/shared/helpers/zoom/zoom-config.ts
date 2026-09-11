
export class ZoomConfig {
  static readonly ZOOM_COUNTIES_THRESHOLD = 9;
  static readonly ZOOM_MUNICIPALITIES_THRESHOLD = 11;

  /** Zoom used when clicking a county marker — lands safely inside the municipality layer range. */
  static readonly ZOOM_AFTER_COUNTY_CLICK = 9.5;
  /** Zoom used when clicking a municipality marker — lands safely inside the locations layer range. */
  static readonly ZOOM_AFTER_MUNICIPALITY_CLICK = 11.5;

  static readonly DEFAULT_ZOOM_LEVEL = 6.2;

  static getApiZoomLevel(openLayerZoom: number): number {
    if (openLayerZoom >= 11) {
      return 3;
    } else if (openLayerZoom >= 9) {
      return 2;
    }
    return 1;
  }
}
