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
import { map, shareReplay } from 'rxjs/operators';
import {
  AreaMarkerDto,
  AreaMarkerFeature,
  getAreaTypeName,
} from '@shared/models/area/area-marker.model';
import { AbbreviateNumberHelper } from '@shared/helpers/number/abbreviate-number.helper';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { ApiClientService } from '../api-client.service';
import { LoggingService } from '@shared/logging.service';
import { ValidationService } from '../validation.service';
import { ApiMessages } from '@core/constants/api-messages';

/**
 * NBIC styling configuration for location markers
 */
const NBIC_LOCATION_STYLE = {
  'nbic:style': {
    strokeColor: '#005A71',
    fillColor: 'rgba(0, 90, 113, 0.15)',
    strokeWidth: 2,
    circle: { radius: 6, fillColor: '#005A71', strokeColor: '#D2DDE0', strokeWidth: 2 }
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

  private readonly areasBaseEndpoint = '/api/Search/AreasObservations';
  private readonly locationsEndpoint = '/api/Search/Locations';

  /**
   * Fetches areas from API and converts to GeoJSON format
   */
  private fetchAreaObservations(openLayerZoom: number): Observable<AreaMarkerDto[]> {
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
   * Gets area observations as GeoJSON features for map rendering
   * @param openLayerZoom - Current OpenLayers zoom level (0-18)
   * Note: For zoom > 12 (API level 3), use getLocationsAsGeoJson() instead
   */
  getAreasObservationsAsGeoJson(openLayerZoom: number): Observable<AreaMarkerFeature[]> {
    return this.fetchAreaObservations(openLayerZoom).pipe(
      map(areas => this.convertToGeoJsonFeatures(areas)),
      shareReplay(1)
    );
  }

  /**
   * Fetches locations as GeoJSON point features (for high zoom levels)
   */
  getLocationsAsGeoJson(): Observable<AreaMarkerFeature[]> {
    return this.apiClientService.fetchJson<string>(this.locationsEndpoint, { responseType: 'text' }).pipe(
      map((responseText: string) => {
        const parsed = this.apiClientService.parseJsonResponse<unknown>(responseText, AreasService.SERVICE_NAME);
        const features: AreaMarkerFeature[] = this.mapLocationsToGeoJson(parsed);
        this.loggerService.info(`Retrieved ${features.length} location features`, AreasService.SERVICE_NAME);
        return features;
      }),
      shareReplay(1)
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

  private convertToGeoJsonFeatures(areas: AreaMarkerDto[]): AreaMarkerFeature[] {
    const features: AreaMarkerFeature[] = [];

    for (const area of areas) {
      const polygonCoords = parsePolygonWkt(area.wktsPolygon);
      if (!polygonCoords) continue;

      features.push({
        type: 'Feature' as const,
        geometry: { type: 'Polygon' as const, coordinates: polygonCoords as number[][][] },
        properties: {
          id: area.id,
          name: area.name,
          areaTypeId: area.areaTypeId,
          areaTypeName: getAreaTypeName(area.areaTypeId),
          observationCount: area.observationCount ?? null,
          observationCountDisplay: area.observationCount && area.observationCount > 0
            ? AbbreviateNumberHelper.format(area.observationCount)
            : '',
          fid: area.fid,
          isPolygon: true
        }
      });
    }

    return features;
  }
}

