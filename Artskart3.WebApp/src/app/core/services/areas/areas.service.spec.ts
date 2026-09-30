import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { AreasService } from './areas.service';
import { ApiClientService } from '../api-client.service';
import { LanguageService } from '@shared/services/languages/language.service';
import { AreaMarkerFeature } from '@shared/models/area/area-marker.model';

describe('AreasService map styling metadata', () => {
  const postJson = vi.fn();
  let service: AreasService;

  beforeEach(() => {
    postJson.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiClientService, useValue: { postJson, parseJsonResponse: JSON.parse } },
        { provide: LanguageService, useValue: { getLanguage: () => 'en' } },
      ],
    });
    service = TestBed.inject(AreasService);
  });

  it('preserves location IDs, coordinates, and counts without overriding layer styles', async () => {
    postJson.mockReturnValue(of(JSON.stringify({ locations: [[42, 10, 60, 1234]] })));
    const collection = JSON.parse(await firstValueFrom(service.getLocationsAsGeoJsonString())) as { features: AreaMarkerFeature[] };
    const [feature] = collection.features;
    expect(feature.id).toBe(42);
    expect(feature.geometry).toEqual({ type: 'Point', coordinates: [10, 60] });
    expect(feature.properties.id).toBe(42);
    expect(feature.properties.observationCount).toBe(1234);
    expect(feature.properties.observationCountDisplay).toBeTruthy();
    expect(feature.properties['nbic:style']).toBeUndefined();
  });

  it('preserves polygon identity and geometry without overriding layer styles', async () => {
    postJson.mockReturnValue(of([{ locationId: 42, locality: 'Test', observationCount: 1234, wktPolygon: 'POLYGON ((0 0,10 0,10 10,0 0))' }]));
    const collection = JSON.parse(await firstValueFrom(service.getLocationPolygons())) as { features: AreaMarkerFeature[] };
    const [feature] = collection.features;
    expect(feature.id).toBe(42);
    expect(feature.properties.id).toBe(42);
    expect(feature.properties.isPolygon).toBe(true);
    expect(feature.properties.observationCount).toBe(1234);
    expect(feature.geometry.type).toBe('Polygon');
    expect(feature.properties['nbic:style']).toBeUndefined();
  });

  it('keeps administrative boundary styles and moves count labels to marker metadata', () => {
    const collection = JSON.parse(service.buildAreaGeoJson(
      [{
        id: 1, documentId: 'county-1', fid: 'county-1', name: 'County', areaTypeId: 2, parentFid: '',
        observationCount: 1234, centroid: { x: 4, y: 4 }, wktsPolygon: 'POLYGON ((0 0,10 0,10 10,0 0))',
      }],
      [-1, -1, 11, 11],
    )) as { features: AreaMarkerFeature[] };
    const [boundary, marker] = collection.features;
    expect(boundary.properties['nbic:style']).toEqual({
      strokeColor: 'rgba(10, 109, 188, 0.6)',
      strokeWidth: 1.5,
      fillColor: 'rgba(0, 0, 0, 0)',
    });
    expect(marker.geometry).toEqual({ type: 'Point', coordinates: [4, 4] });
    expect(marker.properties['centroid']).toEqual({ x: 4, y: 4 });
    expect(marker.properties.observationCount).toBe(1234);
    expect(marker.properties.observationCountDisplay).toBeTruthy();
    expect(marker.properties['nbic:style']).toBeUndefined();
  });
});
