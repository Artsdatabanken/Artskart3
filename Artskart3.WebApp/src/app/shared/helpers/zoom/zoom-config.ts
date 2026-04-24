/**
 * Zoom configuration constants for map area visibility
 */
export class ZoomConfig {
  static readonly ZOOM_COUNTIES_THRESHOLD = 9;
  static readonly ZOOM_MUNICIPALITIES_THRESHOLD = 12;

  static readonly SCROLL_SENSITIVITY = 0.1;
  static readonly SCROLL_THROTTLE_MS = 50;
  static readonly ZOOM_ANIMATION_DURATION = 200;

  static readonly DEFAULT_ZOOM_LEVEL = 6.2;
  static readonly MIN_ZOOM = 0;
  static readonly MAX_ZOOM = 18;

  static readonly NEGLIGIBLE_ZOOM_DIFFERENCE = 0.05;
}
