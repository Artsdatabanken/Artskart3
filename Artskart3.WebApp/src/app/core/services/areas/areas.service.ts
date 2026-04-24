/**
 * Areas API Service
 * Fetches area data from backend API and converts to map-ready format
 */

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, catchError, shareReplay, retry } from 'rxjs/operators';
import { environment } from '@env/environment';
import {
  AreaMarkerDto,
  AreaMarkerFeature,
  getAreaTypeName,
} from '@shared/models/area/area-marker.model';
import { AbbreviateNumberHelper } from '@shared/helpers/number/abbreviate-number.helper';

function parsePointWkt(wkt: string | undefined): [number, number] | null {
  if (!wkt) return null;

  const match = wkt.match(/POINT\s*\(\s*([-\d.]+)\s+([-\d.]+)\s*\)/i);
  if (!match) return null;

  const x = parseFloat(match[1]);
  const y = parseFloat(match[2]);

  return isNaN(x) || isNaN(y) ? null : [x, y];
}

@Injectable({
  providedIn: 'root'
})
export class AreasService {
  private readonly areasEndpoint = `${environment.apiUrl}/api/Search/Areas`;
  private areasCache$: Observable<AreaMarkerDto[]> | null = null;
  private markersCache$: Observable<AreaMarkerFeature[]> | null = null;

  constructor(private http: HttpClient) {}

  getAreas(): Observable<AreaMarkerDto[]> {
    if (!this.areasCache$) {
      this.areasCache$ = this.http.get(this.areasEndpoint, {
        responseType: 'text'
      }).pipe(
        retry({ count: 2, delay: 1000 }),
        map(text => {
          try {
            const areas = JSON.parse(text);
            if (!Array.isArray(areas)) {
              throw new Error('API response is not an array');
            }
            return areas;
          } catch (parseError) {
            const error = parseError instanceof Error ? parseError.message : String(parseError);
            console.error('Failed to parse area data:', error);
            throw new Error(`Failed to parse API response: ${error}`);
          }
        }),
        catchError((error) => {
          console.error('Areas API error:', error);
          return throwError(() => new Error(`Failed to load areas: ${error.message}`));
        }),
        shareReplay(1)
      );
    }
    return this.areasCache$;
  }

  getAreasAsGeoJson(): Observable<AreaMarkerFeature[]> {
    if (!this.markersCache$) {
      this.markersCache$ = this.getAreas().pipe(
        map(areas => this.convertToGeoJsonFeatures(areas)),
        shareReplay(1)
      );
    }
    return this.markersCache$;
  }

  getAreasByType(areaTypeIds: number[]): Observable<AreaMarkerFeature[]> {
    return this.getAreasAsGeoJson().pipe(
      map(features =>
        features.filter(f => areaTypeIds.includes(f.properties.areaTypeId))
      )
    );
  }

  clearCache(): void {
    this.areasCache$ = null;
    this.markersCache$ = null;
  }

  private convertToGeoJsonFeatures(areas: AreaMarkerDto[]): AreaMarkerFeature[] {
    return areas
      .map((area) => {
        const utm33NCoords = parsePointWkt(area.centroid);

        if (!utm33NCoords) {
          return null;
        }

        const areaTypeName = getAreaTypeName(area.areaTypeId);

        return {
          type: 'Feature',
          geometry: {
            type: 'Point',
            coordinates: utm33NCoords
          },
          properties: {
            id: area.id,
            name: area.name,
            areaTypeId: area.areaTypeId,
            areaTypeName,
            observationCount: area.observationCount ?? null,
            observationCountDisplay: area.observationCount && area.observationCount > 0
              ? AbbreviateNumberHelper.format(area.observationCount)
              : '',
            fid: area.fid
          }
        };
      })
      .filter((feature): feature is AreaMarkerFeature => feature !== null);
  }
}

