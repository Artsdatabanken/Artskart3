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
export type Position = number[];

export interface GeoJsonObject {
  type: string;
  bbox?: undefined;
}

export interface Point extends GeoJsonObject {
  type:string;
  coordinates: Position;
}

export type Geometry = Point ;


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

export type GeoJsonTypes = GeoJSON["type"];
export type GeoJSON<G extends Geometry | null = Geometry, P = GeoJsonProperties> =
    | G
    | Feature<G, P>
    | FeatureCollection<G, P>;

  export interface Feature<G extends Geometry | null = Geometry, P = GeoJsonProperties> extends GeoJsonObject {
    type: "Feature";
    geometry: G;
    id?: string | number | undefined;
    properties: P;
}

export type BuildOptions<T> = PointBuildOptions<T>;
export interface PointBuildOptions<T> extends CommonOptions<T> {
  kind: 'Point';
  geometry?:Geometry;
  getPoint?: PointGetter<T>;
}

export interface CommonOptions<T> {
  id?: IdGetter<T>;
  props?: PropsGetter<T>;
  skipInvalid?: boolean;  // default true
  filterNull?: boolean;   // default true
}

export type IdGetter<T> = (item: T, index: number) => string | number | undefined;
export type PropsGetter<T> = (item: T, index: number) => GeoJsonProperties | undefined;
export type GeoJsonProperties = { [name: string]: any } | null;
export type PointGetter<T> = (item: T, index: number) => { lon: number; lat: number } | null;
export interface FeatureCollection<G extends Geometry | null = Geometry, P = GeoJsonProperties> extends GeoJsonObject {
  type: "FeatureCollection";
  features: Array<Feature<G, P>>;
}
