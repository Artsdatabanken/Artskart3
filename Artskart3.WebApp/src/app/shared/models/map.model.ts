import { LayerDef } from 'nbic-map-component';
//import { Geometry, Point, Polygon } from 'ol/geom';
import { FeatureCollection } from 'geojson';

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

export interface Geometry{
  coordinates: number[];
  type: string // "Point" or other?
}

export interface Property{
  ObservationCount: number, 
  MaxCategory: number
}

export interface Site{
  id:number;
  geometry:Geometry;
  properties:Property
  type: string
}

