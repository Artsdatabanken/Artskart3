
export class ZoomConfig {
  static readonly ZOOM_COUNTIES_THRESHOLD = 9;
  static readonly ZOOM_MUNICIPALITIES_THRESHOLD = 11;

  static readonly SCROLL_SENSITIVITY = 0.1;

  static readonly DEFAULT_ZOOM_LEVEL = 6.2;
  static readonly MIN_ZOOM = 0;
  static readonly MAX_ZOOM = 18;

  static constrainZoom(zoom: number, minZoom: number = ZoomConfig.MIN_ZOOM, maxZoom: number = ZoomConfig.MAX_ZOOM): number {
    return Math.max(minZoom, Math.min(maxZoom, zoom));
  }

  static calculateZoomDelta(deltaY: number, sensitivity: number = ZoomConfig.SCROLL_SENSITIVITY): number {
    return deltaY > 0 ? -sensitivity : sensitivity;
  }

  static getApiZoomLevel(openLayerZoom: number): number {
    if (openLayerZoom >= 0 && openLayerZoom <= 8) {
      return 1;
    } else if (openLayerZoom >= 9 && openLayerZoom <= 11) {
      return 2;
    } else if (openLayerZoom > 11) {

      return 3;
    }
    return 1;
  }
}
