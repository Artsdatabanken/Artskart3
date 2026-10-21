export class ZoomConfig {
  static readonly ZOOM_COUNTIES_THRESHOLD = 9;
  static readonly ZOOM_MUNICIPALITIES_THRESHOLD = 11;

  /** Zoom used when clicking a county marker — lands safely inside the municipality layer range. */
  static readonly ZOOM_AFTER_COUNTY_CLICK = 9.5;
  /** Zoom used when clicking a municipality marker — lands safely inside the locations layer range. */
  static readonly ZOOM_AFTER_MUNICIPALITY_CLICK = 11.5;

  static readonly DEFAULT_ZOOM_LEVEL = 6.2;

  /** Below this filtered location count the map skips area layers and clusters locations directly. */
  static readonly DIRECT_CLUSTER_MAX_LOCATIONS = 10000;
  /** Clicked clusters with more locations than this zoom to fit their extent instead of opening the popover. */
  static readonly CLUSTER_CLICK_MAX_LOCATIONS = 15;
  /** Zoom step used when a cluster's members all share the same point. */
  static readonly CLUSTER_CLICK_ZOOM_STEP = 2;
  /** Share of the view's width and height added on each side of a locations fetch, so small pans reuse the result. */
  static readonly LOCATIONS_FETCH_MARGIN = 0.25;
  /**
   * Max locations per request. Clustering can only be turned off while the filter matches at most
   * this many, so unclustered results are never cut.
   */
  static readonly MAX_LOCATIONS_PER_FETCH = 100000;
  /** Max location polygons per request. The API caps them at 5000. */
  static readonly MAX_POLYGONS_PER_FETCH = 5000;

  static getApiZoomLevel(openLayerZoom: number): number {
    if (openLayerZoom >= 11) {
      return 3;
    } else if (openLayerZoom >= 9) {
      return 2;
    }
    return 1;
  }
}
