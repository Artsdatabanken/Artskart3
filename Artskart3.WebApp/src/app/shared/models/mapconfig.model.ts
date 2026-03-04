import { LayerDef } from 'nbic-map-component';

export interface MapControlsConfig {
  geoLocation: boolean;
  zoomToGeoLocation: boolean;
  fullScreen: boolean;
  drawPolygon: boolean;
  mapClick: boolean;
  enableCreate: boolean;
  layerSelector: boolean;
  overlaysInlayerSelector: boolean;
  scaleLine: boolean;
  locationSearchResult: boolean;
  legend: boolean;
  siteInfo: boolean;
}

export interface MapConfig {
  id: string;
  center: number[];
  zoom: number;
  maxZoom: number;
  size?: 'Large' | 'Small';
  controls: Partial<MapControlsConfig>;
  wmtsLayers?: LayerDef[];
  wfsLayers?: LayerDef[];
  tabIndex?: number;
  polygonType?: 'box' | 'custom';
}

export interface WMTSOptions {
  name: string;
  attribution?: string;
  opacity: number;
  url: string;
  layer: string;
  matrixSet: string;
  format: 'image/png' | 'image/jpeg';
  projection: string;
  style: string;
  wrapX: boolean;
  customExtent?: number[];
}

export interface WFSOptions {
  url: string;
  typeName: string;
  featureType: string;
  srs: string;
  schema: string;
  style: string;
  disableFill: boolean;
  strokeWidth: number;
  minZoom: number;
  maxZoom?: number;
}
