import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { provideRouter, Router } from '@angular/router';
import { Subject, throwError, of } from 'rxjs';

import { NbicMapComponent, LayerDef } from '@artsdatabanken/nbic-map-component';
import { MapComponent } from './map.component';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ApiZoomLevel } from './map.types';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { AreasService, LocationSearchFilter } from '@core/services/areas/areas.service';
import type { LocationCountResult } from '@shared/types/api.types';
import { FilterStateService } from '@shared/services/filter-state/filter-state.service';
import { MapViewService } from '@shared/services/map-view/map-view.service';
import type { Signal } from '@angular/core';
import { Feature } from 'ol';
import Point from 'ol/geom/Point';
import Polygon from 'ol/geom/Polygon';
import SimpleGeometry from 'ol/geom/SimpleGeometry';
import VectorLayer from 'ol/layer/Vector';
import OlMap from 'ol/Map';
import MapBrowserEvent from 'ol/MapBrowserEvent';
import { Style } from 'ol/style';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationListInfoDto } from '@shared/types/api.types';
import { SharedMapService } from '@shared/services/shared-map.service';
import { MapFeatureStyles } from './map-feature-styles';
import { ArtskartZoomControl } from './controls/zoom.control';

describe('MapComponent lifecycle', () => {
  let fixture: ComponentFixture<MapComponent>;
  let component: MapComponent;
  const lifecycle = () =>
    component as unknown as {
      initializeMap(): void;
      onMapReady(): void;
    };

  beforeEach(async () => {
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [MapComponent],
      providers: [provideTranslateService(), provideRouter([]), { provide: SharedMapService, useValue: { getNibToken: () => '' } }],
    }).compileComponents();
    fixture = TestBed.createComponent(MapComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('still initializes once after the configured delay while alive', () => {
    const initialize = vi.spyOn(lifecycle(), 'initializeMap').mockImplementation(() => undefined);
    vi.advanceTimersByTime(MAP_CONFIG.initDelay - 1);
    expect(initialize).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(initialize).toHaveBeenCalledOnce();
    vi.advanceTimersByTime(MAP_CONFIG.initDelay);
    expect(initialize).toHaveBeenCalledOnce();
  });

  it('cancels pending initialization when destroyed before the delay expires', async () => {
    const initialize = vi.spyOn(lifecycle(), 'initializeMap');
    const emit = vi.spyOn(component.mapReadyAction, 'emit');
    const warn = vi.spyOn(console, 'warn');
    fixture.destroy();
    await vi.advanceTimersByTimeAsync(MAP_CONFIG.initDelay + 100);
    expect(initialize).not.toHaveBeenCalled();
    expect(component.map).toBeUndefined();
    expect(emit).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalledWith(expect.stringContaining('NG0953'));
  });

  it('ignores late initialization and ready callbacks after destruction', () => {
    const emit = vi.spyOn(component.mapReadyAction, 'emit');
    const warn = vi.spyOn(console, 'warn');
    fixture.destroy();
    lifecycle().initializeMap();
    lifecycle().onMapReady();
    expect(component.map).toBeUndefined();
    expect(emit).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalledWith(expect.stringContaining('NG0953'));
  });

  it('disposes an initialized map without reactivating it on a late ready callback', () => {
    const map = { destroy: vi.fn(), activateHoverInfo: vi.fn() };
    Object.assign(component, { map });
    const emit = vi.spyOn(component.mapReadyAction, 'emit');
    fixture.destroy();
    lifecycle().onMapReady();
    expect(map.destroy).toHaveBeenCalledOnce();
    expect(map.activateHoverInfo).not.toHaveBeenCalled();
    expect(emit).not.toHaveBeenCalled();
  });
});

describe('MapComponent', () => {
  let component: MapComponent;
  let fixture: ComponentFixture<MapComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MapComponent, MapToolbarComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(MapComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('linked feature styling', () => {
    const location = (id: number) => new Feature({ id, geometry: new Point([id, id]), observationCount: 2 });
    const polygon = (id: number) => new Feature({ id, geometry: new Polygon([[[0, 0], [10, 0], [10, 10], [0, 0]]]) });
    const cluster = (...ids: number[]) => new Feature({ geometry: new Point([0, 0]), features: ids.map(location) });
    let layers: Map<string, LayerDef>;
    let redraw: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      const tokens = document.createElement('div').style;
      for (const [token, value] of [
        ['--adb-surface-accent-primary', '#005A71'],
        ['--adb-surface-accent-hover', '#004557'],
        ['--adb-border-base-subtle', '#D2DDE0'],
        ['--adb-border-base-strong', '#768083'],
        ['--adb-border-brand-4', '#F8AE00'],
      ]) tokens.setProperty(token, value);
      component['featureStyles'] = new MapFeatureStyles(tokens);
      component['mapReady'] = true;
      const layer = new VectorLayer();
      redraw = vi.spyOn(layer, 'changed');
      layers = new Map();
      const map: Partial<NbicMapComponent> = {
        addLayer: (definition) => { layers.set(definition.id, definition); },
        getLayerById: () => layer,
        updateGeoJSONLayer: vi.fn(),
        destroy: vi.fn(),
      };
      component.map = map as NbicMapComponent;
      component['setupAreaMarkerLayers']();
    });

    function select(ids: number[]): void {
      component.observationSelection.set({ key: 1, kind: 'selection', locationIds: ids, geometryLabel: 'Test' });
      component.showObservationList.set(true);
    }

    function hover(hits: { feature: Feature; layerId: string }[]): void {
      const map: Partial<OlMap> = {
        forEachFeatureAtPixel: (_pixel, callback) => {
          for (const hit of hits) {
            const layer = new VectorLayer({ properties: { id: hit.layerId } });
            const geometry = hit.feature.getGeometry();
            if (!(geometry instanceof SimpleGeometry)) throw new Error('Expected simple geometry');
            const result = callback(hit.feature, layer, geometry);
            if (result) return result;
          }
          return undefined;
        },
      };
      component['updateMarkerHover'](map as OlMap, [0, 0]);
    }

    function render(layerId: string, feature: Feature, clustered = false): Style[] {
      const layer = layers.get(layerId);
      const definition = clustered ? layer?.cluster?.style : layer?.style;
      if (!definition || !('type' in definition) || definition.type !== 'raw' || typeof definition.options.instance !== 'function') {
        throw new Error('Expected raw style function');
      }
      const result: unknown = definition.options.instance(feature, 1);
      if (!Array.isArray(result) || !result.every((style): style is Style => style instanceof Style)) throw new Error('Expected style array');
      return result;
    }

    it('links polygon hover to its containing cluster without highlighting unrelated member polygons', () => {
      hover([{ feature: polygon(1), layerId: 'location-polygons' }]);
      expect(component['locationFeatureState'](polygon(1))).toBe('hover');
      expect(component['locationFeatureState'](location(1))).toBe('hover');
      expect(component['locationFeatureState'](cluster(1, 2))).toBe('hover');
      expect(component['locationFeatureState'](polygon(2))).toBe('default');
      hover([{ feature: cluster(1, 2), layerId: 'area-markers-locations' }]);
      expect(component['locationFeatureState'](polygon(1))).toBe('hover');
      expect(component['locationFeatureState'](polygon(2))).toBe('hover');
    });

    it('hovers only the topmost interactive hit and clears hover on exit', () => {
      hover([
        { feature: location(1), layerId: 'area-markers-locations' },
        { feature: polygon(2), layerId: 'location-polygons' },
      ]);
      expect(component['locationFeatureState'](location(1))).toBe('hover');
      expect(component['locationFeatureState'](polygon(2))).toBe('default');
      component['clearMarkerHover']();
      expect(component['locationFeatureState'](location(1))).toBe('default');
      hover([{ feature: polygon(2), layerId: 'location-polygons' }]);
      hover([]);
      expect(component['locationFeatureState'](polygon(2))).toBe('default');
    });

    it('ignores administrative outlines and gives count markers hover but never selected styles', () => {
      const area = new Feature({ fid: 'county-1', centroid: { x: 0, y: 0 }, observationCountDisplay: '12 k', geometry: new Point([0, 0]) });
      hover([
        { feature: polygon(1), layerId: 'area-markers-counties' },
        { feature: area, layerId: 'area-markers-counties' },
      ]);
      select([1]);
      const styles = render('area-markers-counties', area);
      expect(styles).toBe(component['featureStyles']!.marker('area', 'hover', '12 k'));
      expect(styles[1].getText()?.getText()).toBe('12 k');
      expect(component['locationFeatureState'](location(2))).toBe('default');
      component['clearMarkerHover']();
      expect(render('area-markers-counties', area)).toBe(component['featureStyles']!.marker('area', 'default', '12 k'));
    });

    it('makes selection win over hover and highlights any cluster member without selecting other polygons', () => {
      hover([{ feature: cluster(1, 2), layerId: 'area-markers-locations' }]);
      select([1]);
      expect(component['locationFeatureState'](location(1))).toBe('selected');
      expect(component['locationFeatureState'](polygon(1))).toBe('selected');
      expect(component['locationFeatureState'](cluster(1, 2))).toBe('selected');
      expect(component['locationFeatureState'](polygon(2))).toBe('hover');
      component['clearMarkerHover']();
      expect(component['locationFeatureState'](polygon(2))).toBe('default');
      expect(component.observationSelection()?.locationIds).toEqual([1]);
    });

    it('wires singleton, cluster, polygon, and detail-highlight styles without changing labels or pickability', () => {
      select([1]);
      const styles = component['featureStyles']!;
      expect(render('area-markers-locations', location(1))).toBe(styles.marker('location', 'selected'));
      expect(render('area-markers-locations', cluster(1), true)).toBe(styles.marker('cluster', 'selected', '2'));
      const grouped = render('area-markers-locations', cluster(1, 2), true);
      expect(grouped[1].getText()?.getText()).toBe('4');
      expect(render('location-polygons', polygon(1))).toBe(styles.polygon('selected'));
      expect(render('observation-details-highlight', location(2))).toBe(styles.marker('observation', 'selected'));
      expect(layers.get('observation-details-highlight')?.pickable).toBe(false);
      expect(layers.get('area-markers-locations')?.cluster?.keepSingleAsCluster).toBe(true);
    });

    it('retains selection through request states, suspends it while details are open, then clears it on dismissal', async () => {
      select([1]);
      for (const state of ['loading', 'ready', 'error'] as const) {
        component.observationRequestState.set(state);
        expect(component['locationFeatureState'](location(1))).toBe('selected');
      }
      await TestBed.inject(Router).navigate([], { queryParams: { observationId: 42 } });
      expect(component['locationFeatureState'](location(1))).toBe('default');
      expect(component['locationFeatureState'](polygon(1))).toBe('default');
      await TestBed.inject(Router).navigate([], { queryParams: { observationId: 43 } });
      expect(component['locationFeatureState'](polygon(1))).toBe('default');
      await TestBed.inject(Router).navigate([], { queryParams: {} });
      expect(component['locationFeatureState'](location(1))).toBe('selected');
      expect(component['locationFeatureState'](polygon(1))).toBe('selected');
      component.closeObservationList();
      expect(component['locationFeatureState'](location(1))).toBe('default');
      expect(component['locationFeatureState'](polygon(1))).toBe('default');
    });

    it('styles replacement features and regrouped clusters by current IDs, not object identity', () => {
      select([1, 2]);
      hover([{ feature: location(3), layerId: 'area-markers-locations' }]);
      component['applyGeoJsonToLayer'](ApiZoomLevel.LocationPoints, '{"type":"FeatureCollection","features":[]}');
      expect(component['locationFeatureState'](location(3))).toBe('default');
      expect(component['locationFeatureState'](cluster(1, 3))).toBe('selected');
      expect(component['locationFeatureState'](cluster(2, 4))).toBe('selected');
      expect(component['locationFeatureState'](polygon(1))).toBe('selected');
      select([3]);
      expect(component['locationFeatureState'](polygon(1))).toBe('default');
      expect(component['locationFeatureState'](cluster(1, 3))).toBe('selected');
    });

    it('redraws on selection changes without refetching data and skips unchanged hover state', async () => {
      await fixture.whenStable();
      redraw.mockClear();
      select([1]);
      await fixture.whenStable();
      expect(redraw).toHaveBeenCalled();
      expect(component.map.updateGeoJSONLayer).not.toHaveBeenCalled();
      hover([{ feature: location(2), layerId: 'area-markers-locations' }]);
      redraw.mockClear();
      hover([{ feature: location(2), layerId: 'area-markers-locations' }]);
      expect(redraw).not.toHaveBeenCalled();
    });

    it('clears transient hover on drag, camera motion, and viewport exit and removes listeners on destruction', () => {
      const map = new OlMap({ target: document.createElement('div'), controls: [], interactions: [] });
      const control = new ArtskartZoomControl({ zoomInTipLabel: 'Zoom in', zoomOutTipLabel: 'Zoom out' });
      map.addControl(control);
      component['zoomControl'] = control;
      const feature = new Feature<Point>({ id: 1, geometry: new Point([0, 0]) });
      const layer = new VectorLayer({ properties: { id: 'area-markers-locations' } });
      const hitTest = vi.spyOn(map, 'forEachFeatureAtPixel')
        .mockImplementation((_pixel, callback) => callback(feature, layer, feature.getGeometry()!));
      const removeListener = vi.spyOn(map.getViewport(), 'removeEventListener');
      component['setupMarkerCursor']();
      const pointerMove = (dragging = false) => {
        const event = new MapBrowserEvent('pointermove', map, new PointerEvent('pointermove'), dragging);
        event.pixel = [0, 0];
        map.dispatchEvent(event);
      };

      pointerMove();
      expect(component['locationFeatureState'](feature)).toBe('hover');
      expect(map.getTargetElement().style.cursor).toBe('pointer');
      pointerMove(true);
      expect(component['locationFeatureState'](feature)).toBe('default');
      pointerMove();
      map.dispatchEvent('movestart');
      expect(component['locationFeatureState'](feature)).toBe('default');
      pointerMove();
      map.getViewport().dispatchEvent(new Event('pointerleave'));
      expect(component['locationFeatureState'](feature)).toBe('default');
      expect(map.getTargetElement().style.cursor).toBe('');

      fixture.destroy();
      hitTest.mockClear();
      pointerMove();
      expect(hitTest).not.toHaveBeenCalled();
      expect(removeListener).toHaveBeenCalledWith('pointerleave', component['clearMarkerHover']);
      map.dispose();
    });
  });

  describe('observation highlighting', () => {
    it.each([true, false])('updates and clears only the highlight with an OL view available: %s', async (withView) => {
      const map = { updateGeoJSONLayer: vi.fn(), setCenter: vi.fn(), setZoom: vi.fn() };
      const view = { centerOn: vi.fn(), setCenter: vi.fn(), setZoom: vi.fn(), animate: vi.fn(), fit: vi.fn() };
      Object.assign(component, {
        map,
        mapReady: true,
        mapVisible: true,
        zoomControl: withView ? { getMap: () => ({ getView: () => view, getSize: () => [1000, 800] }) } : undefined,
      });

      for (const [east, north] of [
        [353063, 7201367],
        [400000, 7300000],
      ]) {
        component.highlightObservation({ east, north });
        await fixture.whenStable();
        expect(map.updateGeoJSONLayer).toHaveBeenLastCalledWith(
          'observation-details-highlight',
          JSON.stringify({
            type: 'FeatureCollection',
            features: [{ type: 'Feature', geometry: { type: 'Point', coordinates: [east, north] }, properties: {} }],
          }),
          { mode: 'replace', dataProjection: 'EPSG:25833' },
        );
      }

      component.highlightObservation(null);
      await fixture.whenStable();
      expect(map.updateGeoJSONLayer).toHaveBeenLastCalledWith(
        'observation-details-highlight',
        '{"type":"FeatureCollection","features":[]}',
        { mode: 'replace', dataProjection: 'EPSG:25833' },
      );
      expect(map.setCenter).not.toHaveBeenCalled();
      expect(map.setZoom).not.toHaveBeenCalled();
      for (const cameraAction of Object.values(view)) expect(cameraAction).not.toHaveBeenCalled();
    });

    it('waits for a ready, visible map before updating the highlight', () => {
      const updateGeoJSONLayer = vi.fn();
      Object.assign(component, { map: { updateGeoJSONLayer }, mapReady: false, mapVisible: true });
      const point = { east: 353063, north: 7201367 };
      component.highlightObservation(point);
      expect(updateGeoJSONLayer).not.toHaveBeenCalled();
      Object.assign(component, { mapReady: true, mapVisible: false });
      component.highlightObservation(point);
      expect(updateGeoJSONLayer).not.toHaveBeenCalled();
      Object.assign(component, { mapVisible: true });
      component.highlightObservation(point);
      expect(updateGeoJSONLayer).toHaveBeenCalledOnce();
    });
  });

  describe('observation selection', () => {
    const access = () =>
      component as unknown as {
        handleLocationClick: (payload: unknown) => void;
        setupObservationRequests: () => void;
        locationClick$: Subject<number[]>;
      };
    const point = (id: number) => new Feature({ geometry: new Point([353063, 7201367]), id });
    const click = (features: { layerId: string; feature: Feature }[]) =>
      access().handleLocationClick({ features, clickCoordinate: [353000, 7201300] });

    it('uses feature coordinates instead of pointer coordinates', () => {
      click([{ layerId: 'area-markers-locations', feature: point(1) }]);
      expect(component.observationSelection()?.geometryLabel).toBe('UTM33 353063, 7201367');
    });

    it('deduplicates location IDs and prefers polygon geometry over its point', () => {
      const polygon = new Feature({ id: 1, geometry: new Polygon([[[1, 2], [3, 4], [5, 6], [1, 2]]]) });
      click([{ layerId: 'area-markers-locations', feature: point(1) }, { layerId: 'location-polygons', feature: polygon }]);
      expect(component.observationSelection()?.locationIds).toEqual([1]);
      expect(component.observationSelection()?.kind).toBe('polygon');
      expect(component.observationSelection()?.geometryLabel).toBe('POLYGON((1 2,3 4,5 6,1 2))');
    });

    it('represents multiple locations as a selection', () => {
      click([{ layerId: 'area-markers-locations', feature: point(1) }, { layerId: 'area-markers-locations', feature: point(2) }]);
      expect(component.observationSelection()?.kind).toBe('selection');
    });

    it('cancels stale and dismissed requests and retries a failed current selection', () => {
      const first = new Subject<ObservationListInfoDto[]>();
      const second = new Subject<ObservationListInfoDto[]>();
      const service = TestBed.inject(ObservationService);
      const request = vi.spyOn(service, 'getObservationByLocation')
        .mockReturnValueOnce(first).mockReturnValueOnce(second)
        .mockReturnValueOnce(throwError(() => new Error('Failed')))
        .mockReturnValueOnce(of([{ id: 4 }]));
      access().setupObservationRequests();
      click([{ layerId: 'area-markers-locations', feature: point(1) }]);
      expect(component.observationRequestState()).toBe('loading');
      click([{ layerId: 'area-markers-locations', feature: point(2) }]);
      expect(first.observed).toBe(false);
      first.next([{ id: 1 }]);
      expect(component.observationList()).toEqual([]);
      component.closeObservationList();
      expect(second.observed).toBe(false);
      second.next([{ id: 2 }]);
      expect(component.showObservationList()).toBe(false);
      click([{ layerId: 'area-markers-locations', feature: point(3) }]);
      expect(component.observationRequestState()).toBe('error');
      component.retryObservations();
      expect(component.observationRequestState()).toBe('ready');
      expect(component.observationList()).toEqual([{ id: 4 }]);
      expect(request).toHaveBeenLastCalledWith([3], expect.anything());
    });

    it('dismisses and cancels the selection when search filters change', async () => {
      const pending = new Subject<ObservationListInfoDto[]>();
      vi.spyOn(TestBed.inject(ObservationService), 'getObservationByLocation').mockReturnValue(pending);
      access().setupObservationRequests();
      click([{ layerId: 'area-markers-locations', feature: point(1) }]);
      TestBed.inject(FilterStateService).selectedCategoryIds.set([12]);
      await fixture.whenStable();
      expect(component.showObservationList()).toBe(false);
      expect(component.observationSelection()).toBeNull();
      expect(pending.observed).toBe(false);
    });
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
        zoomControl?: { getMap: () => { getView: () => { getZoom: () => number; getProjection: () => string; fit: (e: unknown, o: unknown) => void; animate: (o: unknown) => void } } | null };
        locationClick$: Subject<number[]>;
      };

    let fitSpy: ReturnType<typeof vi.fn<(e: unknown, o: unknown) => void>>;
    let animateSpy: ReturnType<typeof vi.fn<(o: unknown) => void>>;
    let clickedIds: number[][];

    const setupView = (zoom: number) => {
      fitSpy = vi.fn<(e: unknown, o: unknown) => void>();
      animateSpy = vi.fn<(o: unknown) => void>();
      accessPrivate(component).zoomControl = { getMap: () => ({ getView: () => ({ getZoom: () => zoom, getProjection: () => MAP_CONFIG.projection, fit: fitSpy, animate: animateSpy }) }) };
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

  describe('requested extent (saved filters)', () => {
    let fitExtentSpy: ReturnType<typeof vi.fn>;
    let mapView: MapViewService;
    let rect = { width: 0, height: 0 };

    const priv = (c: MapComponent) =>
      c as unknown as {
        mapReady: boolean;
        mapEl: { nativeElement: { getBoundingClientRect: () => { width: number; height: number } } };
        tryApplyRequestedExtent: () => void;
      };

    beforeEach(() => {
      mapView = TestBed.inject(MapViewService);
      fitExtentSpy = vi.fn();
      component.map = { fitExtent: fitExtentSpy } as unknown as NbicMapComponent;
      priv(component).mapReady = true;
      priv(component).mapEl = { nativeElement: { getBoundingClientRect: () => rect } };
    });

    it('should fit to a requested extent when the map is visible', () => {
      rect = { width: 800, height: 600 };
      mapView.requestFit([1, 2, 3, 4]);
      TestBed.tick();

      expect(fitExtentSpy).toHaveBeenCalledWith([1, 2, 3, 4], 0);
      expect(mapView.requestedExtent()).toBeNull();
    });

    it('should wait while the map is hidden and fit once it becomes visible', () => {
      rect = { width: 0, height: 0 };
      mapView.requestFit([1, 2, 3, 4]);
      TestBed.tick();

      expect(fitExtentSpy).not.toHaveBeenCalled();
      expect(mapView.requestedExtent()).toEqual([1, 2, 3, 4]);

      rect = { width: 800, height: 600 };
      priv(component).tryApplyRequestedExtent();

      expect(fitExtentSpy).toHaveBeenCalledWith([1, 2, 3, 4], 0);
    });

    it('should not fit before the map is ready', () => {
      rect = { width: 800, height: 600 };
      priv(component).mapReady = false;
      mapView.requestFit([1, 2, 3, 4]);
      TestBed.tick();

      expect(fitExtentSpy).not.toHaveBeenCalled();
      expect(mapView.requestedExtent()).toEqual([1, 2, 3, 4]);
    });
  });
});
