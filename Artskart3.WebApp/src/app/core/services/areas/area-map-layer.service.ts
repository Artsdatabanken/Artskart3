/**
 * Area Map Layer Service
 * Creates and manages map layers for area markers with zoom-based visibility
 */

import { Injectable, inject } from '@angular/core';
import {
  AreaMarkerFeature,
  AREA_TYPE_CONFIG,
  getAreaTypeColor
} from '@shared/models/area/area-marker.model';
import { MAP_CONFIG } from '@shared/config/map.config';
import { LoggingService } from '@shared/logging.service';

export interface MapLayer {
  id: string;
  kind: 'tile' | 'raster' | 'vector';
  type: 'fill' | 'circle' | 'symbol' | 'line';
  source: {
    type: 'geojson';
    data: {
      type: 'FeatureCollection';
      features: AreaMarkerFeature[];
    };
  };
  layout?: Record<string, unknown>;
  paint?: Record<string, unknown>;
  filter?: unknown[];
  minzoom?: number;
  maxzoom?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AreaMapLayerService {

  private readonly logger = inject(LoggingService);

  private readonly CIRCLE_PAINT = {
    'circle-opacity': MAP_CONFIG.circle.opacity,
    'circle-stroke-width': MAP_CONFIG.circle.strokeWidth,
    'circle-stroke-color': MAP_CONFIG.circle.strokeColor,
    'circle-stroke-opacity': MAP_CONFIG.circle.strokeOpacity
  } as const;

  createMarkerLayer(
    layerId: string,
    features: AreaMarkerFeature[],
    visibleAreaTypes?: number[]
  ): MapLayer {
    const filterByType = visibleAreaTypes?.length
      ? ['in', ['get', 'areaTypeId'], ['literal', visibleAreaTypes]]
      : null;

    const layer: MapLayer = {
      id: layerId,
      kind: 'vector',
      type: 'circle',
      source: {
        type: 'geojson',
        data: {
          type: 'FeatureCollection',
          features
        }
      },
      paint: {
        ...this.CIRCLE_PAINT,
        'circle-radius': this.getCircleRadiusExpression(),
        'circle-color': this.getCircleColorExpression()
      }
    };

    if (filterByType) {
      layer.filter = filterByType;
    }

    return layer;
  }

  private getCircleRadiusExpression(): unknown[] {
    const conditions: unknown[] = ['case'];
    const areaConfigs = Object.values(AREA_TYPE_CONFIG) as { id: number; name: string; color: string }[];

    for (const config of areaConfigs) {
      conditions.push(
        ['==', ['get', 'areaTypeId'], config.id],
        ['interpolate', ['linear'], ['zoom'], ...MAP_CONFIG.markerRadiusInterpolation[config.id as keyof typeof MAP_CONFIG.markerRadiusInterpolation] || [6, 6, 11, 16]]
      );
    }

    conditions.push(8);
    return conditions;
  }

  private getCircleColorExpression(): unknown[] {
    const conditions: unknown[] = ['case'];
    const areaConfigs = Object.values(AREA_TYPE_CONFIG) as { id: number; name: string; color: string }[];

    for (const config of areaConfigs) {
      conditions.push(
        ['==', ['get', 'areaTypeId'], config.id],
        getAreaTypeColor(config.id)
      );
    }

    conditions.push(MAP_CONFIG.colors.default);
    return conditions;
  }
}
