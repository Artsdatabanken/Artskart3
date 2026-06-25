/**
 * Areas API Service
 * Fetches area data from backend API and converts to map-ready format
 *
 * Zoom Level Strategy:
 * - Zoom 0-8: API zoom level 1 (counties + broad sea areas)
 * - Zoom 9-12: API zoom level 2 (municipalities + detailed sea areas)
 * - Zoom > 12: Calls locations route instead of area observations
 */

import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  AreaMarkerDto,
  AreaMarkerFeature,
} from '@shared/models/area/area-marker.model';
import { AbbreviateNumberHelper } from '@shared/helpers/number/abbreviate-number.helper';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { ApiClientService } from '../api-client.service';
import { LoggingService } from '@shared/logging.service';
import { ValidationService } from '../validation.service';
import { ApiMessages } from '@core/constants/api-messages';

/**
 * NBIC styling configuration for location markers (solid circle)
 */
const NBIC_LOCATION_STYLE = {
  'nbic:style': {
    pointRadius: 8,
    fillColor: '#005A71',
    strokeColor: '#D2DDE0',
    strokeWidth: 2,
  }
};

/**
 * Parses WKT POLYGON format to GeoJSON coordinates
 * @example "POLYGON ((1 2, 3 4, 5 6, 1 2))" => [[[1, 2], [3, 4], [5, 6], [1, 2]]]
 */
function parsePolygonWkt(wkt: string | undefined): number[][][] | null {
  if (!wkt) return null;

  const match = wkt.match(/POLYGON\s*\(\((.*)\)\)/i);
  if (!match) return null;

  try {
    const coordinatesStr = match[1];
    const points = coordinatesStr.split(',').map(p => p.trim());

    const coordinates = points
      .map(point => {
        const nums = point.split(/\s+/).map(n => parseFloat(n));
        if (nums.length !== 2 || isNaN(nums[0]) || isNaN(nums[1])) {
          return null;
        }
        return [nums[0], nums[1]] as [number, number];
      })
      .filter((p): p is [number, number] => p !== null);

    if (coordinates.length < 3) return null;
    return [coordinates];
  } catch {
    return null;
  }
}

@Injectable({
  providedIn: 'root'
})
export class AreasService {
  private static readonly SERVICE_NAME = 'AreasService';

  private readonly apiClientService: ApiClientService = inject(ApiClientService);
  private readonly loggerService: LoggingService = inject(LoggingService);
  private readonly validationService: ValidationService = inject(ValidationService);

  private readonly areasBaseEndpoint = '/api/Search/AreaMarkers';
  private readonly locationsEndpoint = '/api/Search/Locations';

  /**
   * Fetches areas from API and converts to GeoJSON format
   */
  private fetchAreaMarkers(openLayerZoom: number): Observable<AreaMarkerDto[]> {
    const validation = this.validationService.validateZoomLevel(openLayerZoom);
    if (!validation.valid) {
      throw new Error(validation.error || ApiMessages.Errors.InvalidParameters);
    }

    const apiZoomLevel = ZoomConfig.getApiZoomLevel(validation.normalized!);

    return this.apiClientService
      .fetchJson<string>(`${this.areasBaseEndpoint}?zoomLevel=${apiZoomLevel}`, { responseType: 'text' })
      .pipe(
        map(responseText => {
          const areas = this.apiClientService.parseJsonResponse<AreaMarkerDto[]>(responseText, AreasService.SERVICE_NAME);
          this.loggerService.info(`Retrieved ${Array.isArray(areas) ? areas.length : 0} areas for zoom level ${apiZoomLevel}`, AreasService.SERVICE_NAME);
          return Array.isArray(areas) ? areas : [];
        })
      );
  }

  /**
   * Gets area observations as a serialized GeoJSON FeatureCollection string
   * with per-feature `nbic:style` for direct use with `updateGeoJSONLayer`.
   * Includes both polygon boundaries and centroid marker points.
   */
  getAreaMarkersAsGeoJson(openLayerZoom: number): Observable<string> {
    return this.fetchAreaMarkers(openLayerZoom).pipe(
      map(areas => this.buildAreaFeatureCollection(areas))
    );
  }

  /**
   * Fetches locations and returns two separate GeoJSON FeatureCollection strings:
   * - `points`: single-coordinate localities in EPSG:4326 (for the locations layer)
   * - `polygons`: polygon/surface localities in EPSG:25833 (for the location polygons layer)
   */
  getLocationsGeoJson(): Observable<{ points: string; polygons: string }> {
    return this.apiClientService.fetchJson<string>(this.locationsEndpoint, { responseType: 'text' }).pipe(
      map((responseText: string) => {
        const parsed = this.apiClientService.parseJsonResponse<unknown>(responseText, AreasService.SERVICE_NAME);
        const { pointFeatures, polygonFeatures } = this.mapLocationsToGeoJson(parsed);
        this.loggerService.info(
          `Retrieved ${pointFeatures.length} point locations, ${polygonFeatures.length} polygon location features`,
          AreasService.SERVICE_NAME
        );
        return {
          points: JSON.stringify({ type: 'FeatureCollection', features: pointFeatures }),
          polygons: JSON.stringify({ type: 'FeatureCollection', features: polygonFeatures }),
        };
      })
    );
  }

  /**
   * Maps API location response to separate point and polygon GeoJSON feature arrays.
   * Polygon localities (with WktPolygon property) go to polygonFeatures;
   * single-point localities go to pointFeatures.
   */
  private mapLocationsToGeoJson(response: unknown): { pointFeatures: AreaMarkerFeature[]; polygonFeatures: unknown[] } {
    const locations = this.normalizeLocationResponse(response);
    if (!Array.isArray(locations) || locations.length === 0) {
      return { pointFeatures: [], polygonFeatures: [] };
    }

    const pointFeatures: AreaMarkerFeature[] = [];
    const polygonFeatures: unknown[] = [];

    for (const location of locations) {
      const props = (location['properties'] as Record<string, unknown>) || location;
      const wktPolygon = props['WktPolygon'] as string | undefined;
    console.log('Processing location:', { id: location['id'], name: props['Locality'] ?? props['locality'] ?? props['name'], wktPolygon });
      if (wktPolygon) {
        const polyFeatures = this.createLocationPolygonFeatures(location, wktPolygon);
        polygonFeatures.push(...polyFeatures);
      } else {
        const pointFeature = this.createLocationFeature(location);
        if (pointFeature) pointFeatures.push(pointFeature);
      }
    }

    return { pointFeatures, polygonFeatures };
  }

  private normalizeLocationResponse(response: unknown): Record<string, unknown>[] {
    if (Array.isArray(response)) {
      return response as Record<string, unknown>[];
    }

    if (typeof response === 'object' && response !== null) {
      const obj = response as Record<string, unknown>;
      if (Array.isArray(obj['features'])) return obj['features'] as Record<string, unknown>[];
      if (Array.isArray(obj['value'])) return obj['value'] as Record<string, unknown>[];
      if (Array.isArray(obj['data'])) return obj['data'] as Record<string, unknown>[];
    }

    return [];
  }

  private createLocationFeature(location: Record<string, unknown>): AreaMarkerFeature | null {
    try {
      const [lon, lat] = this.extractCoordinates(location);
      if (lon === null || lat === null) return null;

      const props = (location['properties'] as Record<string, unknown>) || location;
      const observationCount = (props['ObservationCount'] ?? props['observationCount'] ?? 0) as number;
      const id = Number(location['id'] ?? props['TaxonId'] ?? props['taxonId']) || 0;
      const name = (props['Locality'] ?? props['locality'] ?? props['name'] ?? `Location ${location['id']}`) as string;
      const taxonId = (props['TaxonId'] ?? props['taxonId']) as number | undefined;

      return {
        type: 'Feature',
        id,
        geometry: { type: 'Point', coordinates: [lon, lat] },
        properties: {
          id,
          name,
          observationCount,
          observationCountDisplay: observationCount ? AbbreviateNumberHelper.format(observationCount) : '',
          isPolygon: false,
          ...(taxonId && { taxonId }),
          ...NBIC_LOCATION_STYLE
        }
      };
    } catch {
      return null;
    }
  }

  /**
   * Creates GeoJSON polygon boundary + centroid marker features for a polygon locality.
   * Polygon coordinates from WKT are in EPSG:25833 (same as area polygons).
   */
  private createLocationPolygonFeatures(location: Record<string, unknown>, wktPolygon: string): unknown[] {
    const polygonCoords = parsePolygonWkt(wktPolygon);
    console.log('Parsed polygon coordinates:', polygonCoords);
    if (!polygonCoords) return [];

    const props = (location['properties'] as Record<string, unknown>) || location;
    const observationCount = (props['ObservationCount'] ?? props['observationCount'] ?? 0) as number;
    const id = Number(location['id'] ?? 0);
    const name = (props['Locality'] ?? props['locality'] ?? `Location ${id}`) as string;
    const formattedCount = observationCount > 0 ? AbbreviateNumberHelper.format(observationCount) : '';
    const centroid = this.calculateCentroid(polygonCoords[0]);

    return [
      // Polygon boundary
      {
        type: 'Feature',
        id,
        geometry: { type: 'Polygon', coordinates: polygonCoords },
        properties: {
          id,
          name,
          observationCount,
          observationCountDisplay: formattedCount,
          isPolygon: true,
          'nbic:style': {
            strokeColor: 'rgba(247, 7, 7, 0.7)',
            strokeWidth: 1.5,
            fillColor: 'rgba(10, 109, 188, 0.1)',
          }
        }
      },
      // Centroid marker with observation count label
      {
        type: 'Feature',
        id: `${id}-centroid`,
        geometry: { type: 'Point', coordinates: centroid },
        properties: {
          id,
          name,
          observationCount,
          observationCountDisplay: formattedCount,
          isPolygon: true,
          'nbic:style': {
            pointRadius: 8,
            fillColor: '#005A71',
            strokeColor: '#D2DDE0',
            strokeWidth: 1.5,
          }
        }
      }
    ];
  }

  private extractCoordinates(location: Record<string, unknown>): [number | null, number | null] {
    const geometry = location['geometry'] as { type?: string; coordinates?: number[] } | undefined;
    if (geometry?.type === 'Point' && geometry.coordinates?.length === 2) {
      const [lon, lat] = geometry.coordinates;
      if (!isNaN(lon) && !isNaN(lat)) {
        return [lon, lat];
      }
    }

    const lat = (location['latitude'] ?? location['lat']) as number | null;
    const lon = (location['longitude'] ?? location['lon']) as number | null;
    if (lat != null && lon != null && !isNaN(lat) && !isNaN(lon)) {
      return [lon, lat];
    }

    return [null, null];
  }

  /**
   * Builds a GeoJSON FeatureCollection string from area DTOs.
   * Each area produces two features:
   * - A polygon with stroke-only style (boundary)
   * - A point at the centroid with an icon + count label
   */
  private buildAreaFeatureCollection(areas: AreaMarkerDto[]): string {
    const features: unknown[] = [];

    for (const area of areas) {
      const polygonCoords = parsePolygonWkt(area.wktsPolygon);
      if (!polygonCoords) continue;

      const count = area.observationCount ?? 0;
      const formattedCount = count > 0 ? AbbreviateNumberHelper.format(count) : '';
      const centroid = this.calculateCentroid(polygonCoords[0]);

      // Polygon boundary feature
      features.push({
        type: 'Feature',
        geometry: { type: 'Polygon', coordinates: polygonCoords },
        properties: {
          id: area.id,
          name: area.name,
          areaTypeId: area.areaTypeId,
          fid: area.fid,
          'nbic:style': {
            strokeColor: 'rgba(10, 109, 188, 0.6)',
            strokeWidth: 1.5,
            fillColor: 'rgba(0, 0, 0, 0)',
          }
        }
      });

      // Centroid marker feature with circle + count label
      features.push({
        type: 'Feature',
        geometry: { type: 'Point', coordinates: centroid },
        properties: {
          id: area.id,
          name: area.name,
          areaTypeId: area.areaTypeId,
          observationCount: count,
          fid: area.fid,
          'nbic:style': {
            pointRadius: 20,
            fillColor: '#005A71',
            strokeColor: '#D2DDE0',
            strokeWidth: 1.5,
            text: {
              label: formattedCount,
              font: 'bold 10px Arial',
              fillColor: '#FFFFFF',
            }
          }
        }
      });
    }

    this.loggerService.info(`Built ${features.length} GeoJSON features from ${areas.length} areas`, AreasService.SERVICE_NAME);
    return JSON.stringify({ type: 'FeatureCollection', features });
  }

  private calculateCentroid(ring: number[][]): [number, number] {
    if (!ring || ring.length === 0) return [0, 0];
    let x = 0, y = 0;
    for (const coord of ring) {
      x += coord[0];
      y += coord[1];
    }
    return [x / ring.length, y / ring.length];
  }
}
