/**
 * Area Map Layer Service
 * Creates and manages map layers for area markers with zoom-based visibility
 */

import { Injectable } from '@angular/core';
import {
  AreaMarkerFeature,
  AREA_TYPE_CONFIG,
  getAreaTypeColor
} from '@shared/models/area/area-marker.model';
import { AreaMarkerPopupTemplate } from '@shared/templates/area-marker-popup.template';
import { MAP_CONFIG } from '@shared/config/map.config';

export interface MapLayer {
  id: string;
  type: 'fill' | 'circle' | 'symbol' | 'line';
  source: {
    type: 'geojson';
    data: {
      type: 'FeatureCollection';
      features: AreaMarkerFeature[];
    };
  };
  layout?: Record<string, any>;
  paint?: Record<string, any>;
  filter?: any[];
  minzoom?: number;
  maxzoom?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AreaMapLayerService {
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

  createLabelLayer(layerId: string, features: AreaMarkerFeature[]): MapLayer {
    const labelFeatures = features.filter(f =>
      f.properties.observationCountDisplay?.trim()
    );

    return {
      id: layerId,
      type: 'symbol',
      source: {
        type: 'geojson',
        data: {
          type: 'FeatureCollection',
          features: labelFeatures
        }
      },
      layout: {
        'text-field': ['get', 'observationCountDisplay'],
        'text-size': this.getObservationCountTextSize(),
        'text-offset': MAP_CONFIG.textOffset,
        'text-anchor': 'center',
        'text-allow-overlap': true,
        'text-ignore-placement': true
      },
      paint: {
        'text-color': MAP_CONFIG.textColor,
        'text-halo-color': MAP_CONFIG.textHaloColor,
        'text-halo-width': 1
      }
    };
  }

  createMarkerPopup(feature: AreaMarkerFeature): string {
    return AreaMarkerPopupTemplate.createPopup(feature);
  }

  private getCircleRadiusExpression(): any {
    const conditions: any[] = ['case'];

    Object.entries(AREA_TYPE_CONFIG).forEach(([, config]) => {
      conditions.push(
        ['==', ['get', 'areaTypeId'], config.id],
        ['interpolate', ['linear'], ['zoom'], ...MAP_CONFIG.markerRadiusInterpolation[config.id as keyof typeof MAP_CONFIG.markerRadiusInterpolation] || [6, 6, 11, 16]]
      );
    });

    conditions.push(8); // default radius
    return conditions;
  }

  private getCircleColorExpression(): any {
    const conditions: any[] = ['case'];

    Object.entries(AREA_TYPE_CONFIG).forEach(([, config]) => {
      conditions.push(
        ['==', ['get', 'areaTypeId'], config.id],
        getAreaTypeColor(config.id)
      );
    });

    conditions.push(MAP_CONFIG.colors.default); // default color
    return conditions;
  }

  private getObservationCountTextSize(): any {
    return [
      'interpolate',
      ['linear'],
      ['zoom'],
      ...MAP_CONFIG.textSize
    ];
  }

  createZoomAwareLayer(layerId: string, features: AreaMarkerFeature[]): MapLayer {
    const layer = this.createMarkerLayer(layerId, features);
    layer.filter = this.buildZoomAwareFilter();
    return layer;
  }

  private buildZoomAwareFilter(): any[] {
    const conditions = Object.values(AREA_TYPE_CONFIG).map(config => [
      'all',
      ['==', ['get', 'areaTypeId'], config.id],
      ['>=', ['zoom'], config.zoomLevels.minZoom],
      ['<=', ['zoom'], config.zoomLevels.maxZoom]
    ]);

    return conditions.length ? ['any', ...conditions] : ['literal', true];
  }
}
