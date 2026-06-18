
export class ZoomConfig {
  static readonly ZOOM_COUNTIES_THRESHOLD = 9;
  static readonly ZOOM_MUNICIPALITIES_THRESHOLD = 11;

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
