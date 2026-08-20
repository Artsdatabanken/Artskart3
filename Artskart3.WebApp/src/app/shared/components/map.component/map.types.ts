import type Feature from 'ol/Feature';
import type BaseLayer from 'ol/layer/Base';

export enum ApiZoomLevel {
  Counties = 1,
  Municipalities = 2,
  LocationPoints = 3
}

/**
 * The nbic-map-component emits these extra fields at runtime, but they are
 * not yet declared in the package's MapEventMap. Cast payload.features to
 * this until the package types catch up.
 */
export interface PointerClickFeature {
  feature: Feature;
  layer: BaseLayer;
  featureId: string;
  layerId: string;
  properties?: Record<string, unknown>;
}

/** Properties set on location point features by AreasService.mapCompactLocationsToGeoJson */
export interface LocationFeatureProperties {
  id: number;
  name: string;
  observationCount: number;
  observationCountDisplay: string;
  isPolygon: boolean;
}
