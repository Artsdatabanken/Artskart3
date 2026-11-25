import type { Extent } from 'ol/extent';

export const MAP_CONSTANTS = {
  OGC_PIXEL_SIZE_METERS: 0.00028, // OGC standard rendering pixel size (0.28 mm).
  OBSERVATION_HIGHLIGHT_ID: 'observation-details-highlight',
  MAP_TYPE_PREFIX: 'map-type:',
  COUNTIES_LAYER_ID: 'area-markers-counties',
  MUNICIPALITIES_LAYER_ID: 'area-markers-municipalities',
  LOCATIONS_LAYER_ID: 'area-markers-locations',
  LOCATION_POLYGONS_LAYER_ID: 'location-polygons',
  SELECTED_AREAS_OVERLAY_ID: 'area-overlay-selected',
  CLICK_ANIMATION_DURATION_MS: 300,
  LOCATION_HIGHLIGHTS_LAYER_ID: 'location-highlights',
  LOCATION_POLYGON_HIGHLIGHTS_LAYER_ID: 'location-polygon-highlights',
  HIT_TOLERANCE_PX: 5,
  CLUSTER_DISTANCE_PX: 60,
  // The UTM 33 tile grid Kartverket and Norge i bilder share: level 0 is one 256 px tile from
  // top left (-2500000, 9045984) at 21664 m/px.
  UTM33_TILE_GRID_EXTENT: [-2500000, 3500000, 3045984, 9045984] as Extent,
  // Norge i bilder's UTM 33 cache has levels 0–17, the finest at 0.165 m/px.
  NIB_UTM33_LEVELS: 18,
}
