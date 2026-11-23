export const MAP_CONFIG = {
  // Initialization
  initDelay: 100,

  // Map setup
  mapId: 'artskart-map',
  projection: 'EPSG:25833',
  center: [300000, 7220000] as [number, number],

  // Zoom
  minZoom: 0,
  maxZoom: 22,
  // OSM only fills in outside the topo coverage, so coarse tiles are enough when zoomed in.
  osmMaxTileZoom: 14,
  tileLoadTimeoutMs: 10000,
};
