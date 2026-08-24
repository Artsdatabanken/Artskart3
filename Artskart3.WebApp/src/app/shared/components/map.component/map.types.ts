import type Feature from 'ol/Feature';
import type BaseLayer from 'ol/layer/Base';

export enum ApiZoomLevel {
  Counties = 1,
  Municipalities = 2,
  LocationPoints = 3
}

export interface PointerClickFeature {
  feature: Feature;
  layer: BaseLayer;
  featureId: string;
  layerId: string;
  properties?: Record<string, unknown>;
}

export interface LocationFeatureProperties {
  id: number;
  name: string;
  observationCount: number;
  observationCountDisplay: string;
  isPolygon: boolean;
}
