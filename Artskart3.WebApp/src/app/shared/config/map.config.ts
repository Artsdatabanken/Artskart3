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
  // The GEBCO background only shows where the topo map has no data, and from about zoom 16 the topo
  // map covers neighbouring countries too. Its relief is coarse, so tiles are enlarged past this zoom.
  backgroundMaxTileZoom: 15,
  // A tile download is given up after this long. Generous, so slow connections can still finish.
  tileLoadTimeoutMs: 60000,
};
