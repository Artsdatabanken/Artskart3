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

/**
 * Klipper en polygonring mot et rektangulært extent (Sutherland-Hodgman).
 * Returnerer den klipte ringen, eller tom array hvis ingenting er synlig.
 */
function clipRingToExtent(ring: number[][], extent: [number, number, number, number]): number[][] {
  let output = ring;

  const edges: { inside: (p: number[]) => boolean; intersect: (a: number[], b: number[]) => number[] }[] = [
    { // Venstre (x >= minX)
      inside: (p) => p[0] >= extent[0],
      intersect: (a, b) => { const t = (extent[0] - a[0]) / (b[0] - a[0]); return [extent[0], a[1] + t * (b[1] - a[1])]; }
    },
    { // Høyre (x <= maxX)
      inside: (p) => p[0] <= extent[2],
      intersect: (a, b) => { const t = (extent[2] - a[0]) / (b[0] - a[0]); return [extent[2], a[1] + t * (b[1] - a[1])]; }
    },
    { // Bunn (y >= minY)
      inside: (p) => p[1] >= extent[1],
      intersect: (a, b) => { const t = (extent[1] - a[1]) / (b[1] - a[1]); return [a[0] + t * (b[0] - a[0]), extent[1]]; }
    },
    { // Topp (y <= maxY)
      inside: (p) => p[1] <= extent[3],
      intersect: (a, b) => { const t = (extent[3] - a[1]) / (b[1] - a[1]); return [a[0] + t * (b[0] - a[0]), extent[3]]; }
    },
  ];

  for (const edge of edges) {
    if (output.length === 0) break;
    const input = output;
    output = [];
    for (let i = 0; i < input.length; i++) {
      const current = input[i];
      const previous = input[(i + input.length - 1) % input.length];
      if (edge.inside(current)) {
        if (!edge.inside(previous)) {
          output.push(edge.intersect(previous, current));
        }
        output.push(current);
      } else if (edge.inside(previous)) {
        output.push(edge.intersect(previous, current));
      }
    }
  }

  return output;
}

/**
 * Beregner arealet og centroid for en enkel polygonring (shoelace-formelen).
 * Returnerer [signertAreal, cx, cy]. Areal er negativt for klokkeretning.
 */
function ringAreaAndCentroid(ring: number[][]): { area: number; cx: number; cy: number } | null {
  if (ring.length < 3) return null;
  let area = 0, cx = 0, cy = 0;
  for (let i = 0; i < ring.length; i++) {
    const j = (i + 1) % ring.length;
    const cross = ring[i][0] * ring[j][1] - ring[j][0] * ring[i][1];
    area += cross;
    cx += (ring[i][0] + ring[j][0]) * cross;
    cy += (ring[i][1] + ring[j][1]) * cross;
  }
  area /= 2;
  if (Math.abs(area) < 1e-10) return null;
  return { area: Math.abs(area), cx: cx / (6 * area), cy: cy / (6 * area) };
}

/**
 * Ray-casting punkt-i-polygon-test.
 */
function pointInRing(point: [number, number], ring: number[][]): boolean {
  let inside = false;
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i];
    const [xj, yj] = ring[j];
    if ((yi > point[1]) !== (yj > point[1]) &&
        point[0] < (xj - xi) * (point[1] - yi) / (yj - yi) + xi) {
      inside = !inside;
    }
  }
  return inside;
}

/**
 * Sikrer at et punkt ligger innenfor en polygonring.
 * Prøver først midtpunkter mellom punktet og hjørnene,
 * deretter midtpunktet av den lengste kanten som fallback.
 */
function ensureInsideRing(point: [number, number], ring: number[][]): [number, number] {
  if (pointInRing(point, ring)) return point;

  let bestPoint: [number, number] | null = null;
  let bestDistSq = Infinity;

  for (const vertex of ring) {
    const mid: [number, number] = [(point[0] + vertex[0]) / 2, (point[1] + vertex[1]) / 2];
    if (pointInRing(mid, ring)) {
      const distSq = (mid[0] - point[0]) ** 2 + (mid[1] - point[1]) ** 2;
      if (distSq < bestDistSq) {
        bestDistSq = distSq;
        bestPoint = mid;
      }
    }
  }

  if (bestPoint) return bestPoint;

  // Siste utvei: midtpunkt av lengste kant
  let longestLenSq = 0;
  let fallback: [number, number] = [ring[0][0], ring[0][1]];
  for (let i = 0; i < ring.length; i++) {
    const j = (i + 1) % ring.length;
    const lenSq = (ring[j][0] - ring[i][0]) ** 2 + (ring[j][1] - ring[i][1]) ** 2;
    if (lenSq > longestLenSq) {
      longestLenSq = lenSq;
      fallback = [(ring[i][0] + ring[j][0]) / 2, (ring[i][1] + ring[j][1]) / 2];
    }
  }
  return fallback;
}

/**
 * Beregner arealvektet centroid av den synlige delen av et polygon/multipolygon
 * klippet mot kartutsnittet. Garanterer at resultatet ligger innenfor
 * en av de klipte polygondelene.
 */
function calculateClippedCentroid(parsed: ParsedGeometry, extent: [number, number, number, number]): [number, number] | null {
  const polygons = parsed.type === 'MultiPolygon'
    ? (parsed.coordinates as number[][][][])
    : [parsed.coordinates as number[][][]];

  const clippedParts: { ring: number[][]; area: number; cx: number; cy: number }[] = [];

  for (const polygon of polygons) {
    const clipped = clipRingToExtent(polygon[0], extent);
    const result = ringAreaAndCentroid(clipped);
    if (!result) continue;
    clippedParts.push({ ring: clipped, ...result });
  }

  if (clippedParts.length === 0) return null;

  // Arealvektet centroid
  let totalArea = 0, weightedX = 0, weightedY = 0;
  for (const part of clippedParts) {
    totalArea += part.area;
    weightedX += part.cx * part.area;
    weightedY += part.cy * part.area;
  }
  const centroid: [number, number] = [weightedX / totalArea, weightedY / totalArea];

  // Bruk centroid direkte hvis den er innenfor en av de klipte polygonene
  for (const part of clippedParts) {
    if (pointInRing(centroid, part.ring)) return centroid;
  }

  // Ellers: bruk centroid av den største synlige delen, sikret innenfor
  const largest = clippedParts.reduce((a, b) => a.area > b.area ? a : b);
  return ensureInsideRing([largest.cx, largest.cy], largest.ring);
}

export interface LocationSearchFilter {
  categoryIds?: number[];
  organizationIds?: number[];
  behaviorIds?: number[];
  basisOfRecordIds?: number[];
  taxonGroupIds?: number[];
  countyIds?: string[];
  municipalityIds?: string[];
  oceanAreaIds?: string[];
  coordinatePrecisionFrom?: number | null;
  coordinatePrecisionTo?: number | null;
  periodFrom?: number | null;
  periodTo?: number | null;
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
   * Henter områdemarkører fra API for gitt zoomnivå.
   * @param filter Valgfritt søkefilter — brukes til dynamisk telling av observasjoner per område
   */
  getAreaMarkers(openLayerZoom: number, filter?: LocationSearchFilter): Observable<AreaMarkerDto[]> {
    const validation = this.validationService.validateZoomLevel(openLayerZoom);
    if (!validation.valid) {
      throw new Error(validation.error || ApiMessages.Errors.InvalidParameters);
    }

    const apiZoomLevel = ZoomConfig.getApiZoomLevel(validation.normalized!);
    const params = new URLSearchParams();
    params.set('zoomLevel', String(apiZoomLevel));

    if (filter) {
      this.appendArrayParam(params, 'CategoryIds', filter.categoryIds);
      this.appendArrayParam(params, 'OrganizationIds', filter.organizationIds);
      this.appendArrayParam(params, 'BehaviorIds', filter.behaviorIds);
      this.appendArrayParam(params, 'BasisOfRecordIds', filter.basisOfRecordIds);
      this.appendArrayParam(params, 'TaxonGroupIds', filter.taxonGroupIds);
      this.appendArrayParam(params, 'CountyIds', filter.countyIds);
      this.appendArrayParam(params, 'MunicipalityIds', filter.municipalityIds);
      this.appendArrayParam(params, 'OceanAreaIds', filter.oceanAreaIds);
      if (filter.coordinatePrecisionFrom != null) params.set('CoordinatePrecision.From', String(filter.coordinatePrecisionFrom));
      if (filter.coordinatePrecisionTo != null) params.set('CoordinatePrecision.To', String(filter.coordinatePrecisionTo));
      if (filter.periodFrom != null) params.set('Period.From', String(filter.periodFrom));
      if (filter.periodTo != null) params.set('Period.To', String(filter.periodTo));
    }

    return this.apiClientService
      .fetchJson<string>(`${this.areasBaseEndpoint}?${params.toString()}`, { responseType: 'text' })
      .pipe(
        map(responseText => {
          const areas = this.apiClientService.parseJsonResponse<AreaMarkerDto[]>(responseText, AreasService.SERVICE_NAME);
          this.loggerService.info(`Retrieved ${Array.isArray(areas) ? areas.length : 0} areas for zoom level ${apiZoomLevel}`, AreasService.SERVICE_NAME);
          return Array.isArray(areas) ? areas : [];
        })
      );
  }

  /**
   * Fetches locations as a serialized GeoJSON FeatureCollection string
   * with per-feature `nbic:style` for direct use with `updateGeoJSONLayer`.
   * @param extent Kartutsnitt [minX, minY, maxX, maxY] i EPSG:25833
   * @param filter Valgfritt søkefilter for lokasjoner
   */
  getLocationsAsGeoJsonString(extent?: [number, number, number, number], filter?: LocationSearchFilter): Observable<string> {
    const params = new URLSearchParams();

    if (extent) {
      const [minX, minY, maxX, maxY] = extent;
      params.set('Envelope.MinX', String(minX));
      params.set('Envelope.MinY', String(minY));
      params.set('Envelope.MaxX', String(maxX));
      params.set('Envelope.MaxY', String(maxY));
    }

    if (filter) {
      this.appendArrayParam(params, 'CategoryIds', filter.categoryIds);
      this.appendArrayParam(params, 'OrganizationIds', filter.organizationIds);
      this.appendArrayParam(params, 'BehaviorIds', filter.behaviorIds);
      this.appendArrayParam(params, 'BasisOfRecordIds', filter.basisOfRecordIds);
      this.appendArrayParam(params, 'TaxonGroupIds', filter.taxonGroupIds);
      this.appendArrayParam(params, 'CountyIds', filter.countyIds);
      this.appendArrayParam(params, 'MunicipalityIds', filter.municipalityIds);
      this.appendArrayParam(params, 'OceanAreaIds', filter.oceanAreaIds);
      if (filter.coordinatePrecisionFrom != null) params.set('CoordinatePrecision.From', String(filter.coordinatePrecisionFrom));
      if (filter.coordinatePrecisionTo != null) params.set('CoordinatePrecision.To', String(filter.coordinatePrecisionTo));
      if (filter.periodFrom != null) params.set('Period.From', String(filter.periodFrom));
      if (filter.periodTo != null) params.set('Period.To', String(filter.periodTo));
    }

    const queryString = params.toString();
    const url = queryString ? `${this.locationsEndpoint}?${queryString}` : this.locationsEndpoint;

    return this.apiClientService.fetchJson<string>(url, { responseType: 'text' }).pipe(
      map((responseText: string) => {
        const parsed = this.apiClientService.parseJsonResponse<unknown>(responseText, AreasService.SERVICE_NAME);
        const features = this.mapLocationsToGeoJson(parsed);
        this.loggerService.info(`Retrieved ${features.length} location features`, AreasService.SERVICE_NAME);
        return JSON.stringify({ type: 'FeatureCollection', features });
      })
    );
  }

  private appendArrayParam(params: URLSearchParams, name: string, values?: (string | number)[]): void {
    if (!values?.length) return;
    for (const v of values) {
      params.append(name, String(v));
    }
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
   * Bygger GeoJSON FeatureCollection fra områdedata.
   * Områder utenfor kartutsnitt ekskluderes.
   */
  buildAreaGeoJson(areas: AreaMarkerDto[], extent: [number, number, number, number]): string {
    const features: unknown[] = [];

    for (const area of areas) {
      const parsed = parseWkt(area.wktsPolygon);
      if (!parsed) continue;

      // Sjekk om området overlapper med kartutsnitt
      const bbox = this.computeBbox(parsed);
      if (!this.bboxOverlaps(bbox, extent)) continue;

      const count = area.observationCount ?? 0;
      const formattedCount = count > 0 ? AbbreviateNumberHelper.format(count) : '';

      // Bruk DB-centroid når hele området er synlig, ellers beregn centroid av synlig del
      const fullyVisible = bbox[0] >= extent[0] && bbox[1] >= extent[1]
        && bbox[2] <= extent[2] && bbox[3] <= extent[3];

      let centroid: [number, number];
      if (fullyVisible) {
        centroid = area.centroid
          ? [area.centroid.x, area.centroid.y]
          : this.calculateCentroid(
              parsed.type === 'MultiPolygon'
                ? (parsed.coordinates as number[][][][])[0][0]
                : (parsed.coordinates as number[][][])[0]
            );
      } else {
        centroid = calculateClippedCentroid(parsed, extent)
          ?? (area.centroid
            ? [area.centroid.x, area.centroid.y]
            : this.calculateCentroid(
                parsed.type === 'MultiPolygon'
                  ? (parsed.coordinates as number[][][][])[0][0]
                  : (parsed.coordinates as number[][][])[0]
              ));
      }

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

  private computeBbox(parsed: ParsedGeometry): [number, number, number, number] {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    const rings = parsed.type === 'MultiPolygon'
      ? (parsed.coordinates as number[][][][]).flatMap(p => p)
      : (parsed.coordinates as number[][][]);
    for (const ring of rings) {
      for (const [x, y] of ring) {
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
      }
    }
    return [minX, minY, maxX, maxY];
  }

  private bboxOverlaps(a: [number, number, number, number], b: [number, number, number, number]): boolean {
    return a[0] <= b[2] && a[2] >= b[0] && a[1] <= b[3] && a[3] >= b[1];
  }
}
