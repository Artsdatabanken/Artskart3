import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { Subject, throwError, of } from 'rxjs';

import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { MapComponent } from './map.component';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ApiZoomLevel } from './map.types';
import { AreasService } from '@core/services/areas/areas.service';
import { FilterStateService } from '@shared/services/filter-state/filter-state.service';

describe('MapComponent', () => {
  let component: MapComponent;
  let fixture: ComponentFixture<MapComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MapComponent, MapToolbarComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService()]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MapComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('applyGeoJsonToLayer', () => {
    let updateGeoJSONLayerSpy: ReturnType<typeof vi.fn>;
    const applyGeoJsonToLayer = (c: MapComponent, zoom: ApiZoomLevel, geojson: string) =>
      (c as unknown as { applyGeoJsonToLayer: (z: number, g: string) => void }).applyGeoJsonToLayer(zoom, geojson);

    beforeEach(() => {
      updateGeoJSONLayerSpy = vi.fn();
      component.map = { updateGeoJSONLayer: updateGeoJSONLayerSpy } as unknown as NbicMapComponent;
    });

    it('should route to counties layer for Counties zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.Counties, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-counties',
        '{"type":"FeatureCollection"}',
        { mode: 'replace' },
      );
    });

    it('should route to municipalities layer for Municipalities zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.Municipalities, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-municipalities',
        '{"type":"FeatureCollection"}',
        { mode: 'replace' },
      );
    });

    it('should route to locations layer with EPSG:4326 projection for LocationPoints zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.LocationPoints, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-locations',
        '{"type":"FeatureCollection"}',
        { mode: 'replace', dataProjection: 'EPSG:4326' },
      );
    });

    it('should not call updateGeoJSONLayer when map is not set', () => {
      component.map = undefined as unknown as NbicMapComponent;

      applyGeoJsonToLayer(component, ApiZoomLevel.Counties, '{}');

      expect(updateGeoJSONLayerSpy).not.toHaveBeenCalled();
    });
  });

  describe('counts fetch pipeline error resilience', () => {
    let areasService: AreasService;
    let updateGeoJSONLayerSpy: ReturnType<typeof vi.fn>;
    let fetchCounts$: Subject<{ requests: { dataZoomLevel: number; apiZoomLevel: number }[]; extent: [number, number, number, number] }>;
    let locationsFetch$: Subject<{ extent: [number, number, number, number]; filter: unknown }>;

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        fetchCounts$: Subject<{ requests: { dataZoomLevel: number; apiZoomLevel: number }[]; extent: [number, number, number, number] }>;
        locationsFetch$: Subject<{ extent: [number, number, number, number]; filter: unknown }>;
        setupCountsFetchPipeline: () => void;
        setupLocationsFetchPipeline: () => void;
        geometryCacheByApiZoom: Map<number, unknown[]>;
        countsCache: Map<string, unknown>;
      };

    beforeEach(() => {
      areasService = TestBed.inject(AreasService);
      updateGeoJSONLayerSpy = vi.fn();
      component.map = { updateGeoJSONLayer: updateGeoJSONLayerSpy } as unknown as NbicMapComponent;

      const priv = accessPrivate(component);
      fetchCounts$ = priv.fetchCounts$;
      locationsFetch$ = priv.locationsFetch$;
      priv.geometryCacheByApiZoom.clear();
      priv.countsCache.clear();
      priv.setupCountsFetchPipeline();
      priv.setupLocationsFetchPipeline();
    });

    it('should continue processing after a service error', () => {
      vi.useFakeTimers();
      const geojson = '{"type":"FeatureCollection","features":[]}';

      const testExtent: [number, number, number, number] = [0, 0, 1000000, 1000000];

      // First call fails (counts fetch with no cached geometries → falls back to getAreaMarkers)
      vi.spyOn(areasService, 'getAreaMarkers').mockReturnValueOnce(
        throwError(() => new Error('503 Service Unavailable'))
      );

      fetchCounts$.next({ requests: [{ dataZoomLevel: ApiZoomLevel.Municipalities, apiZoomLevel: ApiZoomLevel.Municipalities }], extent: testExtent });
      vi.advanceTimersByTime(300);

      expect(updateGeoJSONLayerSpy).not.toHaveBeenCalled();

      // Second call succeeds — pipeline should still be alive
      vi.spyOn(areasService, 'getLocationsAsGeoJsonString').mockReturnValueOnce(of(geojson));

      locationsFetch$.next({ extent: testExtent, filter: {} });
      vi.advanceTimersByTime(300);

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-locations',
        geojson,
        { mode: 'replace', dataProjection: 'EPSG:4326' },
      );

      vi.useRealTimers();
    });
  });

  describe('geometry and counts caching', () => {
    const AREA_TYPE_COUNTY = 2;
    const AREA_TYPE_MUNICIPALITY = 1;
    const AREA_TYPE_OCEAN = 4;

    const area = (fid: string, areaTypeId: number, observationCount = 0) => ({
      id: Number(fid.replace(/\D/g, '')) || 1,
      documentId: fid,
      fid,
      name: `Area ${fid}`,
      areaTypeId,
      parentFid: '',
      syncDateTime: '',
      timeStamp: '',
      isCurrent: true,
      observationCount,
      wktsPolygon: 'POLYGON((0 0, 0 10, 10 10, 10 0, 0 0))',
    });

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        geometryCacheByApiZoom: Map<number, ReturnType<typeof area>[]>;
        countsCache: Map<string, { counts: Map<string, number>; etag: string | null }>;
        seedCountsFromGeometries: (apiZoomLevel: number, areas: ReturnType<typeof area>[]) => void;
        rebuildAreaLayer: (
          apiZoomLevel: number,
          filter: Record<string, unknown>,
          extent: [number, number, number, number],
          pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[],
        ) => void;
        areaSelectionKey: (filter: Record<string, unknown>) => string;
        countsCacheKey: (zoomLevel: number, selectionKey: string) => string;
      };

    beforeEach(() => {
      component.map = { updateGeoJSONLayer: vi.fn() } as unknown as NbicMapComponent;
      const priv = accessPrivate(component);
      priv.geometryCacheByApiZoom.clear();
      priv.countsCache.clear();
    });

    it('should make ocean areas available in the municipalities geometry cache', () => {
      const priv = accessPrivate(component);
      priv.seedCountsFromGeometries(ApiZoomLevel.Counties, [
        area('03', AREA_TYPE_COUNTY, 100),
        area('91', AREA_TYPE_OCEAN, 50),
      ]);

      priv.seedCountsFromGeometries(ApiZoomLevel.Municipalities, [area('0301', AREA_TYPE_MUNICIPALITY, 100)]);

      const municipalityGeometries = priv.geometryCacheByApiZoom.get(ApiZoomLevel.Municipalities) ?? [];
      expect(municipalityGeometries.map(a => a.fid)).toEqual(['0301', '91']);
    });

    it('should not duplicate ocean areas already present at the municipality level', () => {
      const priv = accessPrivate(component);
      priv.seedCountsFromGeometries(ApiZoomLevel.Counties, [area('91', AREA_TYPE_OCEAN, 50)]);

      priv.seedCountsFromGeometries(ApiZoomLevel.Municipalities, [
        area('0301', AREA_TYPE_MUNICIPALITY, 100),
        area('91', AREA_TYPE_OCEAN, 50),
      ]);

      const fids = (priv.geometryCacheByApiZoom.get(ApiZoomLevel.Municipalities) ?? []).map(a => a.fid);
      expect(fids).toEqual(['0301', '91']);
    });

    it('should render selected areas with zero counts from cache without refetching', () => {
      const priv = accessPrivate(component);
      const filterState = TestBed.inject(FilterStateService);
      filterState.selectedCategoryIds.set([1]);

      const filter = { oceanAreaIds: ['91'], municipalityIds: ['0301'] };
      priv.geometryCacheByApiZoom.set(ApiZoomLevel.Municipalities, [
        area('0301', AREA_TYPE_MUNICIPALITY),
        area('91', AREA_TYPE_OCEAN),
      ]);
      // Backend utelater områder uten treff — '91' mangler bevisst
      priv.countsCache.set(priv.countsCacheKey(ApiZoomLevel.Municipalities, priv.areaSelectionKey(filter)), {
        counts: new Map([['0301', 5]]),
        etag: null,
      });

      const pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[] = [];
      priv.rebuildAreaLayer(ApiZoomLevel.Municipalities, filter, [0, 0, 1000000, 1000000], pendingFetches);

      expect(pendingFetches).toEqual([]);
      filterState.selectedCategoryIds.set([]);
    });

    it('should refetch counts when the area selection changes', () => {
      const priv = accessPrivate(component);
      const filterState = TestBed.inject(FilterStateService);
      filterState.selectedCategoryIds.set([1]);

      priv.geometryCacheByApiZoom.set(ApiZoomLevel.Municipalities, [area('0301', AREA_TYPE_MUNICIPALITY)]);
      priv.countsCache.set(priv.countsCacheKey(ApiZoomLevel.Municipalities, priv.areaSelectionKey({ municipalityIds: ['0301'] })), {
        counts: new Map([['0301', 5]]),
        etag: null,
      });

      const pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[] = [];
      priv.rebuildAreaLayer(ApiZoomLevel.Municipalities, { municipalityIds: ['0302'] }, [0, 0, 1000000, 1000000], pendingFetches);

      expect(pendingFetches).toEqual([
        { dataZoomLevel: ApiZoomLevel.Municipalities, apiZoomLevel: ApiZoomLevel.Municipalities },
      ]);
      filterState.selectedCategoryIds.set([]);
    });
  });

  describe('zero-count area rendering', () => {
    const AREA_TYPE_MUNICIPALITY = 1;

    const area = (fid: string, observationCount = 0, parentFid = '') => ({
      id: 1,
      documentId: fid,
      fid,
      name: `Area ${fid}`,
      areaTypeId: AREA_TYPE_MUNICIPALITY,
      parentFid,
      syncDateTime: '',
      timeStamp: '',
      isCurrent: true,
      observationCount,
      wktsPolygon: 'POLYGON((0 0, 0 10, 10 10, 10 0, 0 0))',
    });

    const mergeCountsIntoAreas = (
      c: MapComponent,
      areas: ReturnType<typeof area>[],
      counts: Map<string, number>,
      filter: Record<string, unknown>,
    ) =>
      (c as unknown as {
        mergeCountsIntoAreas: (
          a: ReturnType<typeof area>[],
          counts: Map<string, number>,
          f: Record<string, unknown>,
        ) => ReturnType<typeof area>[];
      }).mergeCountsIntoAreas(areas, counts, filter);

    it('should hide unselected areas without observations', () => {
      const result = mergeCountsIntoAreas(
        component,
        [area('0301'), area('0302')],
        new Map([['0301', 7]]),
        {},
      );

      expect(result.map(a => a.fid)).toEqual(['0301']);
    });

    it('should keep explicitly selected areas with zero observations', () => {
      const result = mergeCountsIntoAreas(
        component,
        [area('0301'), area('91')],
        new Map([['0301', 7]]),
        { municipalityIds: ['0301'], oceanAreaIds: ['91'] },
      );

      expect(result.map(a => ({ fid: a.fid, count: a.observationCount }))).toEqual([
        { fid: '0301', count: 7 },
        { fid: '91', count: 0 },
      ]);
    });

    it('should hide zero-count children of a selected county', () => {
      const result = mergeCountsIntoAreas(
        component,
        [area('0301', 0, '03'), area('0302', 0, '03')],
        new Map([['0302', 4]]),
        { countyIds: ['03'] },
      );

      expect(result.map(a => a.fid)).toEqual(['0302']);
    });
  });
});
