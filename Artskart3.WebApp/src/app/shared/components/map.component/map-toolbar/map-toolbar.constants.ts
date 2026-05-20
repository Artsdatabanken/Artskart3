
export enum ToolbarAction {
  ZOOM_IN = 'plus',
  ZOOM_OUT = 'minus',
  GEOLOCATION = 'posisjon',
  FULLSCREEN = 'expand',
  MAP = 'kart',
  LAYERS = 'layers',
  FILTER = 'filter',
  POLYGON = 'polygon',
}

export const TOOLBAR_ACTIONS = {
  ZOOM_IN: ToolbarAction.ZOOM_IN,
  ZOOM_OUT: ToolbarAction.ZOOM_OUT,
  GEOLOCATION: ToolbarAction.GEOLOCATION,
  FULLSCREEN: ToolbarAction.FULLSCREEN,
  MAP: ToolbarAction.MAP,
  LAYERS: ToolbarAction.LAYERS,
  FILTER: ToolbarAction.FILTER,
  POLYGON: ToolbarAction.POLYGON,
} as const;
