import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { Subject, throwError, of } from 'rxjs';

import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { MapComponent } from './map.component';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ApiZoomLevel } from './map.types';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { AreasService, LocationSearchFilter } from '@core/services/areas/areas.service';
import type { LocationCountResult } from '@shared/types/api.types';
import { FilterStateService } from '@shared/services/filter-state/filter-state.service';
import type { Signal } from '@angular/core';

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

  describe('stale response guard', () => {
    const AREA_TYPE_MUNICIPALITY = 1;
    const testExtent: [number, number, number, number] = [0, 0, 1000000, 1000000];

    const area = (fid: string, observationCount = 0) => ({
      id: 1,
      documentId: fid,
      fid,
      name: `Area ${fid}`,
      areaTypeId: AREA_TYPE_MUNICIPALITY,
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
        setupCountsFetchPipeline: () => void;
        rebuildWithExtent: (filter: Record<string, unknown>, extent: [number, number, number, number]) => void;
        areaSelectionKey: (filter: Record<string, unknown>) => string;
        countsCacheKey: (zoomLevel: number, selectionKey: string) => string;
      };

    it('should not apply a slow filtered response that arrives after the filter is cleared', () => {
      const areasService = TestBed.inject(AreasService);
      const filterState = TestBed.inject(FilterStateService);
      const priv = accessPrivate(component);
      const updateSpy = vi.fn();
      component.map = {
        updateGeoJSONLayer: updateSpy,
        getCamera: () => ({ zoom: 10 }),
        setLayerVisibility: vi.fn(),
      } as unknown as NbicMapComponent;

      priv.geometryCacheByApiZoom.set(ApiZoomLevel.Municipalities, [area('0301', 100)]);
      priv.setupCountsFetchPipeline();

      // Aktivt attributtfilter → antall må hentes fra backend (treg spørring)
      filterState.selectedCategoryIds.set([1]);
      const staleResponse$ = new Subject<{
        counts: { fid: string; observationCount: number }[] | null;
        etag: string | null;
        notModified: boolean;
      }>();
      vi.spyOn(areasService, 'getAreaCounts').mockReturnValue(staleResponse$.asObservable());
      const staleCacheKey = priv.countsCacheKey(ApiZoomLevel.Municipalities, priv.areaSelectionKey({}));

      priv.rebuildWithExtent({}, testExtent);
      expect(areasService.getAreaCounts).toHaveBeenCalledTimes(1);

      // Brukeren trykker «Tøm filter» mens spørringen pågår. Ufiltrerte antall
      // finnes i geometri-cachen, så kartet oppdateres synkront — ingen ny
      // spørring sendes, og den gamle blir heller ikke kansellert av switchMap.
      filterState.clearAll();
      priv.rebuildWithExtent({}, testExtent);

      const municipalityCalls = () => updateSpy.mock.calls.filter((call) => call[0] === 'area-markers-municipalities');
      expect(municipalityCalls().at(-1)?.[1]).toContain('"observationCount":100');

      // Det trege, filtrerte svaret ankommer — det skal ikke overstyre kartet
      staleResponse$.next({ counts: [{ fid: '0301', observationCount: 3 }], etag: null, notModified: false });

      expect(municipalityCalls().at(-1)?.[1]).toContain('"observationCount":100');
      // ... men antallene caches likevel, så samme filter er instant neste gang
      expect(priv.countsCache.get(staleCacheKey)?.counts.get('0301')).toBe(3);
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

  describe('filter effect', () => {
    it('should not re-trigger the filter effect when the location count updates', async () => {
      const priv = component as unknown as {
        mapReady: boolean;
        locationCountResult: { set: (v: { count: number; truncated: boolean } | null) => void };
        locationCountFetch$: Subject<unknown>;
      };
      priv.mapReady = true;
      const nextSpy = vi.spyOn(priv.locationCountFetch$, 'next');

      priv.locationCountResult.set({ count: 1000, truncated: false });
      fixture.detectChanges();
      await fixture.whenStable();

      expect(nextSpy).not.toHaveBeenCalled();
    });
  });

  describe('locations fetch coverage', () => {
    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        rebuildWithExtent: (filter: Record<string, unknown>, extent: [number, number, number, number]) => void;
        setupLocationsFetchPipeline: () => void;
      };

    it('should not refetch locations when panning within the already fetched extent', () => {
      vi.useFakeTimers();
      try {
        const areasService = TestBed.inject(AreasService);
        const getLocations = vi
          .spyOn(areasService, 'getLocationsAsGeoJsonString')
          .mockReturnValue(of('{"type":"FeatureCollection","features":[]}'));
        vi.spyOn(areasService, 'getLocationPolygons').mockReturnValue(of('{"type":"FeatureCollection","features":[]}'));
        component.map = {
          updateGeoJSONLayer: vi.fn(),
          getCamera: () => ({ zoom: 12 }),
          setLayerVisibility: vi.fn(),
        } as unknown as NbicMapComponent;
        const priv = accessPrivate(component);
        priv.setupLocationsFetchPipeline();

        priv.rebuildWithExtent({}, [0, 0, 1000, 1000]);
        vi.advanceTimersByTime(300);
        expect(getLocations).toHaveBeenCalledTimes(1);

        // Pan innenfor det hentede utsnittet — ingen ny henting
        priv.rebuildWithExtent({}, [100, 100, 900, 900]);
        vi.advanceTimersByTime(300);
        expect(getLocations).toHaveBeenCalledTimes(1);

        // Utsnittet stikker utenfor — ny henting
        priv.rebuildWithExtent({}, [-50, 0, 1000, 1000]);
        vi.advanceTimersByTime(300);
        expect(getLocations).toHaveBeenCalledTimes(2);

        // Samme utsnitt med endret filter — ny henting
        priv.rebuildWithExtent({ taxonIds: [1] }, [0, 0, 1000, 1000]);
        vi.advanceTimersByTime(300);
        expect(getLocations).toHaveBeenCalledTimes(3);
      } finally {
        vi.useRealTimers();
      }
    });
  });

  describe('location count pipeline', () => {
    it('should ignore a stale count response from a previous filter', () => {
      vi.useFakeTimers();
      try {
        const areasService = TestBed.inject(AreasService);
        const filterState = TestBed.inject(FilterStateService);
        const priv = component as unknown as {
          locationCountFetch$: Subject<LocationSearchFilter>;
          locationCountResult: Signal<LocationCountResult | null>;
          locationFilter: () => LocationSearchFilter;
          setupLocationCountPipeline: () => void;
        };
        priv.setupLocationCountPipeline();

        const responseA = new Subject<LocationCountResult>();
        const responseB = new Subject<LocationCountResult>();
        vi.spyOn(areasService, 'getLocationCount')
          .mockReturnValueOnce(responseA.asObservable())
          .mockReturnValueOnce(responseB.asObservable());

        filterState.selectedCategoryIds.set([1]);
        priv.locationCountFetch$.next(priv.locationFilter());
        vi.advanceTimersByTime(200);
        expect(areasService.getLocationCount).toHaveBeenCalledTimes(1);

        // Filteret endres før svar A ankommer — forespørsel B ligger i debounce-vinduet,
        // så switchMap har ennå ikke kansellert A.
        filterState.selectedCategoryIds.set([2]);
        priv.locationCountFetch$.next(priv.locationFilter());
        responseA.next({ count: 10, truncated: false });

        expect(priv.locationCountResult()).toBeNull();

        vi.advanceTimersByTime(200);
        responseB.next({ count: 5, truncated: false });

        expect(priv.locationCountResult()).toEqual({ count: 5, truncated: false });
      } finally {
        TestBed.inject(FilterStateService).clearAll();
        vi.useRealTimers();
      }
    });
  });

  describe('direct cluster mode', () => {
    const testExtent: [number, number, number, number] = [0, 0, 1000000, 1000000];

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        rebuildWithExtent: (filter: Record<string, unknown>, extent: [number, number, number, number]) => void;
        locationCountResult: { set: (v: { count: number; truncated: boolean } | null) => void };
        locationsFetch$: Subject<{ extent: [number, number, number, number]; filter: unknown }>;
      };

    let setLayerVisibilitySpy: ReturnType<typeof vi.fn>;
    let locationsFetched: unknown[];

    beforeEach(() => {
      setLayerVisibilitySpy = vi.fn();
      locationsFetched = [];
      component.map = {
        updateGeoJSONLayer: vi.fn(),
        getCamera: () => ({ zoom: 6.2 }),
        setLayerVisibility: setLayerVisibilitySpy,
      } as unknown as NbicMapComponent;
      accessPrivate(component).locationsFetch$.subscribe((v) => locationsFetched.push(v));
    });

    // Telling nøyaktig på terskelen gir direkte modus (count <= DIRECT_MAX)
    const BELOW_THRESHOLD = ZoomConfig.DIRECT_CLUSTER_MAX_LOCATIONS;
    const ABOVE_THRESHOLD = ZoomConfig.DIRECT_CLUSTER_MAX_LOCATIONS + 1;

    it('should show locations and hide area layers at low zoom when count is below threshold', () => {
      accessPrivate(component).locationCountResult.set({ count: BELOW_THRESHOLD, truncated: false });

      accessPrivate(component).rebuildWithExtent({}, testExtent);

      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-locations', true);
      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('location-polygons', true);
      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-counties', false);
      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-municipalities', false);
      expect(locationsFetched.length).toBe(1);
    });

    it('should keep area layer behavior when count is above threshold', () => {
      accessPrivate(component).locationCountResult.set({ count: ABOVE_THRESHOLD, truncated: false });

      accessPrivate(component).rebuildWithExtent({}, testExtent);

      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-locations', false);
      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-counties', true);
      expect(locationsFetched.length).toBe(0);
    });

    it('should keep area layer behavior while count is unknown', () => {
      accessPrivate(component).locationCountResult.set(null);

      accessPrivate(component).rebuildWithExtent({}, testExtent);

      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-counties', true);
      expect(locationsFetched.length).toBe(0);
    });

    it('should suppress the locations fetch in direct mode while a recount is pending', () => {
      const priv = component as unknown as { locationCountPending: boolean };
      accessPrivate(component).locationCountResult.set({ count: BELOW_THRESHOLD, truncated: false });
      priv.locationCountPending = true;

      accessPrivate(component).rebuildWithExtent({}, testExtent);

      // Modus og synlighet beholdes (ingen flimring), men hentingen utsettes
      expect(setLayerVisibilitySpy).toHaveBeenCalledWith('area-markers-locations', true);
      expect(locationsFetched.length).toBe(0);

      priv.locationCountPending = false;
      accessPrivate(component).rebuildWithExtent({}, testExtent);
      expect(locationsFetched.length).toBe(1);
    });

    it('should fetch locations immediately in direct mode when no recount is pending', () => {
      accessPrivate(component).locationCountResult.set({ count: BELOW_THRESHOLD, truncated: false });

      accessPrivate(component).rebuildWithExtent({}, testExtent);

      expect(locationsFetched.length).toBe(1);
    });
  });

  describe('resolveCursorAtPixel', () => {
    // Avledet fra konfig slik at justering av terskler ikke brekker testene
    const CLUSTER_MAX = ZoomConfig.CLUSTER_CLICK_MAX_LOCATIONS;

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        resolveCursorAtPixel: (olMap: unknown, pixel: [number, number]) => string;
        zoomControl?: { getMap: () => { getView: () => { getZoom: () => number } } | null };
      };

    const olMapWith = (hits: { feature: unknown; layerId: string }[]) => ({
      forEachFeatureAtPixel: (_pixel: unknown, cb: (f: unknown, l: unknown) => unknown) => {
        for (const hit of hits) {
          const result = cb(hit.feature, { get: () => hit.layerId });
          if (result !== undefined) return result;
        }
        return undefined;
      },
    });

    const clusterWith = (memberCount: number) => ({
      get: (key: string) => (key === 'features' ? Array.from({ length: memberCount }, () => ({})) : undefined),
    });

    const setupZoom = (zoom: number) => {
      accessPrivate(component).zoomControl = { getMap: () => ({ getView: () => ({ getZoom: () => zoom }) }) };
    };

    it('should return zoom-in over a cluster above the click threshold', () => {
      setupZoom(12);
      const cursor = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([{ feature: clusterWith(CLUSTER_MAX + 1), layerId: 'area-markers-locations' }]),
        [0, 0],
      );
      expect(cursor).toBe('zoom-in');
    });

    it('should return pointer over a cluster at or below the threshold', () => {
      setupZoom(12);
      const cursor = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([{ feature: clusterWith(CLUSTER_MAX), layerId: 'area-markers-locations' }]),
        [0, 0],
      );
      expect(cursor).toBe('pointer');
    });

    it('should return pointer over a large cluster at max zoom, matching the popover fallback', () => {
      setupZoom(MAP_CONFIG.maxZoom);
      const cursor = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([{ feature: clusterWith(CLUSTER_MAX + 1), layerId: 'area-markers-locations' }]),
        [0, 0],
      );
      expect(cursor).toBe('pointer');
    });

    it('should prioritize zoom-in when a large cluster and a polygon are both hit', () => {
      setupZoom(12);
      const cursor = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([
          { feature: { get: () => undefined }, layerId: 'location-polygons' },
          { feature: clusterWith(CLUSTER_MAX + 1), layerId: 'area-markers-locations' },
        ]),
        [0, 0],
      );
      expect(cursor).toBe('zoom-in');
    });

    it('should return zoom-in over an area centroid marker but not over its polygon outline', () => {
      setupZoom(8);
      const marker = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([{ feature: { get: (k: string) => (k === 'centroid' ? { x: 1, y: 2 } : undefined) }, layerId: 'area-markers-counties' }]),
        [0, 0],
      );
      const outline = accessPrivate(component).resolveCursorAtPixel(
        olMapWith([{ feature: { get: () => undefined }, layerId: 'area-markers-counties' }]),
        [0, 0],
      );
      expect(marker).toBe('zoom-in');
      expect(outline).toBe('');
    });

    it('should return empty cursor when nothing clickable is hit', () => {
      setupZoom(8);
      expect(accessPrivate(component).resolveCursorAtPixel(olMapWith([]), [0, 0])).toBe('');
    });
  });

  describe('cluster click zoom', () => {
    const CLUSTER_MAX = ZoomConfig.CLUSTER_CLICK_MAX_LOCATIONS;

    const clusterFeature = (members: { id: number; coordinate: [number, number] }[]) => ({
      get: (key: string) =>
        key === 'features'
          ? members.map((m) => ({
              getGeometry: () => ({ getCoordinates: () => m.coordinate }),
              getProperties: () => ({ id: m.id, name: `Location ${m.id}`, observationCount: 1, observationCountDisplay: '1', isPolygon: false }),
            }))
          : undefined,
    });

    const payloadFor = (features: unknown[]) => ({ features, clickCoordinate: [250000, 7100000] });

    const accessPrivate = (c: MapComponent) =>
      c as unknown as {
        zoomControl?: { getMap: () => { getView: () => { getZoom: () => number; fit: (e: unknown, o: unknown) => void; animate: (o: unknown) => void } } | null };
        locationClick$: Subject<number[]>;
      };

    let fitSpy: ReturnType<typeof vi.fn<(e: unknown, o: unknown) => void>>;
    let animateSpy: ReturnType<typeof vi.fn<(o: unknown) => void>>;
    let clickedIds: number[][];

    const setupView = (zoom: number) => {
      fitSpy = vi.fn<(e: unknown, o: unknown) => void>();
      animateSpy = vi.fn<(o: unknown) => void>();
      accessPrivate(component).zoomControl = { getMap: () => ({ getView: () => ({ getZoom: () => zoom, fit: fitSpy, animate: animateSpy }) }) };
      clickedIds = [];
      accessPrivate(component).locationClick$.subscribe((ids) => clickedIds.push(ids));
    };

    it('should fit the member extent instead of opening the popover for large clusters', () => {
      setupView(12);
      const size = CLUSTER_MAX + 1;
      const members = Array.from({ length: size }, (_, i) => ({ id: i + 1, coordinate: [100 + i, 200 + i] as [number, number] }));

      (component as unknown as { handleLocationClick: (p: unknown) => void }).handleLocationClick(
        payloadFor([{ layerId: 'area-markers-locations', feature: clusterFeature(members) }]),
      );

      expect(fitSpy).toHaveBeenCalledWith(
        [100, 200, 100 + size - 1, 200 + size - 1],
        expect.objectContaining({ padding: [80, 80, 80, 80] }),
      );
      expect(clickedIds).toEqual([]);
      expect(component.showObservationList()).toBe(false);
    });

    it('should open the popover for clusters at or below the threshold', () => {
      setupView(12);
      const members = Array.from({ length: CLUSTER_MAX }, (_, i) => ({ id: i + 1, coordinate: [100, 200] as [number, number] }));

      (component as unknown as { handleLocationClick: (p: unknown) => void }).handleLocationClick(
        payloadFor([{ layerId: 'area-markers-locations', feature: clusterFeature(members) }]),
      );

      expect(fitSpy).not.toHaveBeenCalled();
      expect(clickedIds).toEqual([Array.from({ length: CLUSTER_MAX }, (_, i) => i + 1)]);
    });

    it('should open the popover when two clusters below the threshold are hit together', () => {
      setupView(12);
      const membersA = Array.from({ length: CLUSTER_MAX }, (_, i) => ({ id: i + 1, coordinate: [100, 200] as [number, number] }));
      const membersB = Array.from({ length: CLUSTER_MAX }, (_, i) => ({ id: i + CLUSTER_MAX + 1, coordinate: [150, 250] as [number, number] }));

      (component as unknown as { handleLocationClick: (p: unknown) => void }).handleLocationClick(
        payloadFor([
          { layerId: 'area-markers-locations', feature: clusterFeature(membersA) },
          { layerId: 'area-markers-locations', feature: clusterFeature(membersB) },
        ]),
      );

      expect(fitSpy).not.toHaveBeenCalled();
      expect(clickedIds).toEqual([Array.from({ length: 2 * CLUSTER_MAX }, (_, i) => i + 1)]);
    });

    it('should step-zoom when all members share the same point', () => {
      setupView(12);
      const members = Array.from({ length: CLUSTER_MAX + 1 }, (_, i) => ({ id: i + 1, coordinate: [100, 200] as [number, number] }));

      (component as unknown as { handleLocationClick: (p: unknown) => void }).handleLocationClick(
        payloadFor([{ layerId: 'area-markers-locations', feature: clusterFeature(members) }]),
      );

      expect(fitSpy).not.toHaveBeenCalled();
      expect(animateSpy).toHaveBeenCalledWith(expect.objectContaining({ center: [100, 200], zoom: 14 }));
      expect(clickedIds).toEqual([]);
    });

    it('should open the popover when already at max zoom', () => {
      setupView(MAP_CONFIG.maxZoom);
      const members = Array.from({ length: CLUSTER_MAX + 1 }, (_, i) => ({ id: i + 1, coordinate: [100 + i, 200] as [number, number] }));

      (component as unknown as { handleLocationClick: (p: unknown) => void }).handleLocationClick(
        payloadFor([{ layerId: 'area-markers-locations', feature: clusterFeature(members) }]),
      );

      expect(fitSpy).not.toHaveBeenCalled();
      expect(clickedIds.length).toBe(1);
    });
  });
});
