import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { Subject, throwError, of } from 'rxjs';

import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { MapComponent } from './map.component';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ApiZoomLevel } from './map.types';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
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

    it('should keep ocean areas delivered by the backend at the municipality level', () => {
      const priv = accessPrivate(component);

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

  describe('handleAreaMarkerClick', () => {
    let animateSpy: ReturnType<typeof vi.fn<(opts: unknown) => void>>;
    let warnSpy: ReturnType<typeof vi.spyOn>;

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        handleAreaMarkerClick: (features: unknown) => void;
        zoomControl?: { getMap: () => { getView: () => { animate: (opts: unknown) => void } } | null };
        logger: { warn: (msg: string, ctx?: string, data?: unknown) => void };
        CLICK_ANIMATION_DURATION_MS: number;
      };

    const animationDuration = () => accessPrivate(component).CLICK_ANIMATION_DURATION_MS;

    const centroid = { x: 250000, y: 7100000 };

    beforeEach(() => {
      animateSpy = vi.fn<(opts: unknown) => void>();
      accessPrivate(component).zoomControl = { getMap: () => ({ getView: () => ({ animate: animateSpy }) }) };
      warnSpy = vi.spyOn(accessPrivate(component).logger, 'warn').mockImplementation(() => undefined);
    });

    it('should animate to the centroid past the county threshold on county marker click', () => {
      accessPrivate(component).handleAreaMarkerClick([{ layerId: 'area-markers-counties', properties: { centroid } }]);

      expect(animateSpy).toHaveBeenCalledWith({
        center: [centroid.x, centroid.y],
        zoom: ZoomConfig.ZOOM_AFTER_COUNTY_CLICK,
        duration: animationDuration(),
      });
    });

    it('should animate to the centroid past the municipality threshold on municipality marker click', () => {
      accessPrivate(component).handleAreaMarkerClick([{ layerId: 'area-markers-municipalities', properties: { centroid } }]);

      expect(animateSpy).toHaveBeenCalledWith({
        center: [centroid.x, centroid.y],
        zoom: ZoomConfig.ZOOM_AFTER_MUNICIPALITY_CLICK,
        duration: animationDuration(),
      });
    });

    it('should ignore the polygon outline feature and use the marker feature in the same layer', () => {
      accessPrivate(component).handleAreaMarkerClick([
          { layerId: 'area-markers-counties', properties: { fid: '03' } },
          { layerId: 'area-markers-counties', properties: { fid: '03', centroid } },
      ]);

      expect(animateSpy).toHaveBeenCalledTimes(1);
      expect(animateSpy).toHaveBeenCalledWith(expect.objectContaining({ center: [centroid.x, centroid.y] }));
    });

    it('should warn and not move the camera when the marker has no valid centroid', () => {
      accessPrivate(component).handleAreaMarkerClick([{ layerId: 'area-markers-counties', properties: {} }]);

      expect(warnSpy).toHaveBeenCalled();
      expect(animateSpy).not.toHaveBeenCalled();
    });

    it('should do nothing when no features were hit', () => {
      accessPrivate(component).handleAreaMarkerClick(null);

      expect(animateSpy).not.toHaveBeenCalled();
      expect(warnSpy).not.toHaveBeenCalled();
    });

    it('should ignore clicks on other layers', () => {
      accessPrivate(component).handleAreaMarkerClick([{ layerId: 'area-markers-locations', properties: { centroid } }]);

      expect(animateSpy).not.toHaveBeenCalled();
    });

    it('should fall back to setCenter/setZoom when no OL view is available', () => {
      accessPrivate(component).zoomControl = undefined;
      const setCenterSpy = vi.fn();
      const setZoomSpy = vi.fn();
      component.map = { setCenter: setCenterSpy, setZoom: setZoomSpy } as unknown as NbicMapComponent;

      accessPrivate(component).handleAreaMarkerClick([{ layerId: 'area-markers-counties', properties: { centroid } }]);

      expect(setCenterSpy).toHaveBeenCalledWith([centroid.x, centroid.y]);
      expect(setZoomSpy).toHaveBeenCalledWith(ZoomConfig.ZOOM_AFTER_COUNTY_CLICK);
    });
  });
});
