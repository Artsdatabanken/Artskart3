export const MAP_CONFIG = {
  // Initialization
  initDelay: 100,

  // Map setup
  mapId: 'artskart-map',
  projection: 'EPSG:25833',
  projectionMatrix: 'utm33n',
  center: [300000, 7220000] as [number, number],

  // Zoom
  minZoom: 0,
  maxZoom: 18,

  // Marker styling
  markerRadiusInterpolation: {
    1: [6, 6, 8, 8, 10, 12, 11, 16],
    2: [4, 8, 6, 12, 7, 16, 11, 22]
  },

  // Text styling
  textSize: [6, 10, 8, 12, 10, 14, 11, 16],
  textOffset: [0, 0],
  textColor: '#ffffff',
  textHaloColor: '#333333',

  // Colors
  colors: {
    default: '#005A71',
    stroke: '#ffffff',
    halo: '#333333'
  },

  // Circle paint
  circle: {
    opacity: 0.9,
    strokeWidth: 2,
    strokeColor: '#ffffff',
    strokeOpacity: 1
  },

  // Tooltip
  tooltipOffset: 15,

  // Layers
  areaMarkersLayer: 'area-markers-layer',
  countiesSuffix: '-counties',
  municipalitiesSuffix: '-municipalities',

  // Area type IDs
  areaTypeIds: {
    municipality: 1,
    county: 2
  }
};
