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

