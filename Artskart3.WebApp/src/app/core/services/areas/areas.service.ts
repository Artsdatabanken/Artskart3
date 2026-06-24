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

interface ParsedGeometry {
  type: 'Polygon' | 'MultiPolygon';
  coordinates: number[][][] | number[][][][];
}

/**
 * Parser én koordinatring fra WKT-format til tallpar.
 * @example "1 2, 3 4, 5 6, 1 2" => [[1, 2], [3, 4], [5, 6], [1, 2]]
 */
function parseRing(ringStr: string): number[][] | null {
  const points = ringStr.split(',').map(p => p.trim());
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
  return coordinates;
}

/**
 * Parser WKT POLYGON og MULTIPOLYGON til GeoJSON-geometri.
 * @example "POLYGON ((1 2, 3 4, 5 6, 1 2))" => { type: 'Polygon', coordinates: [[[1,2],[3,4],[5,6],[1,2]]] }
 * @example "MULTIPOLYGON (((1 2, 3 4, 5 6, 1 2)),((7 8, 9 10, 11 12, 7 8)))" =>
 *   { type: 'MultiPolygon', coordinates: [[[[1,2],[3,4],[5,6],[1,2]]],[[[7,8],[9,10],[11,12],[7,8]]]] }
 */
function parseWkt(wkt: string | undefined): ParsedGeometry | null {
  if (!wkt) return null;

  try {
    // Sjekk MULTIPOLYGON først (inneholder "POLYGON" som delstreng)
    if (/MULTIPOLYGON/i.test(wkt)) {
      const multiMatch = wkt.match(/MULTIPOLYGON\s*\(\(\(([\s\S]*)\)\)\)/i);
      if (!multiMatch) return null;

      const polygonStrings = multiMatch[1].split(/\)\)\s*,\s*\(\(/);
      const polygons: number[][][][] = [];

      for (const polyStr of polygonStrings) {
        const ringStrings = polyStr.split(/\)\s*,\s*\(/);
        const rings: number[][][] = [];
        for (const ringStr of ringStrings) {
          const ring = parseRing(ringStr);
          if (ring) rings.push(ring);
        }
        if (rings.length > 0) polygons.push(rings);
      }

      if (polygons.length === 0) return null;
      return { type: 'MultiPolygon', coordinates: polygons };
    }

    // Standard POLYGON
    const polyMatch = wkt.match(/POLYGON\s*\(\(([\s\S]*)\)\)/i);
    if (!polyMatch) return null;

    const ringStrings = polyMatch[1].split(/\)\s*,\s*\(/);
    const rings: number[][][] = [];
    for (const ringStr of ringStrings) {
      const ring = parseRing(ringStr);
      if (ring) rings.push(ring);
    }

    if (rings.length === 0) return null;
    return { type: 'Polygon', coordinates: rings };
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
   * Fetches locations as a serialized GeoJSON FeatureCollection string
   * with per-feature `nbic:style` for direct use with `updateGeoJSONLayer`.
   * @param extent Kartutsnitt [minX, minY, maxX, maxY] i EPSG:25833
   */
  getLocationsAsGeoJsonString(extent?: [number, number, number, number]): Observable<string> {
    let url = this.locationsEndpoint;
    if (extent) {
      const [minX, minY, maxX, maxY] = extent;
      url += `?Envelope.MinX=${minX}&Envelope.MinY=${minY}&Envelope.MaxX=${maxX}&Envelope.MaxY=${maxY}`;
    }
    return this.apiClientService.fetchJson<string>(url, { responseType: 'text' }).pipe(
      map((responseText: string) => {
        const parsed = this.apiClientService.parseJsonResponse<unknown>(responseText, AreasService.SERVICE_NAME);
        const features = this.mapLocationsToGeoJson(parsed);
        this.loggerService.info(`Retrieved ${features.length} location features`, AreasService.SERVICE_NAME);
        return JSON.stringify({ type: 'FeatureCollection', features });
      })
    );
  }

  /**
   * Maps API location response to GeoJSON features
   */
  private mapLocationsToGeoJson(response: unknown): AreaMarkerFeature[] {
    const locations = this.normalizeLocationResponse(response);
    if (!Array.isArray(locations) || locations.length === 0) {
      return [];
    }

    return locations
      .map(location => this.createLocationFeature(location))
      .filter((f): f is AreaMarkerFeature => f !== null);
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
      const parsed = parseWkt(area.wktsPolygon);
      if (!parsed) continue;

      const count = area.observationCount ?? 0;
      const formattedCount = count > 0 ? AbbreviateNumberHelper.format(count) : '';
      const firstRing = parsed.type === 'MultiPolygon'
        ? (parsed.coordinates as number[][][][])[0][0]
        : (parsed.coordinates as number[][][])[0];
      const centroid = this.calculateCentroid(firstRing);

      // Polygon/MultiPolygon boundary feature
      features.push({
        type: 'Feature',
        geometry: { type: parsed.type, coordinates: parsed.coordinates },
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
