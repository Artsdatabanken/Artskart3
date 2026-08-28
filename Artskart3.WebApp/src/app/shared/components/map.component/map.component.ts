import {
  createMap,
  MapEvents,
  NbicMapComponent,
  nbicMapPresets,
} from '@artsdatabanken/nbic-map-component';
import {
  AfterViewInit,
  Component,
  ElementRef,
  Output,
  EventEmitter,
  ViewChild,
  OnDestroy,
  inject,
  computed,
  effect,
  signal,
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Observable, Subject, EMPTY, merge, concat as rxConcat, defer } from 'rxjs';
import { catchError, debounceTime, map as rxMap, finalize, switchMap, takeUntil, tap } from 'rxjs/operators';
import { AreasService, LocationSearchFilter } from '@core/services/areas/areas.service';
import { AreaMarkerDto } from '@shared/models/area/area-marker.model';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { CommonModule } from '@angular/common';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ImageTile } from 'ol';
import { ApiZoomLevel } from './map.types';
import { FilterStateService, imageFilterToWithImages } from '../../services/filter-state/filter-state.service';
import { AreaService } from '../../services/area/area.service';
import { ArtskartZoomControl } from './controls/zoom.control';
import { ArtskartFullscreenControl } from './controls/fullscreen.control';
import { createGeolocationControl, GeolocationMapControl } from './controls/geolocation.control';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LoadingIndicatorComponent } from '../loading-indicator/loading-indicator.component';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MapToolbarComponent, LoadingIndicatorComponent, TranslateModule],
  templateUrl: './map.component.html',
  styleUrl: './map.component.css',
})
export class MapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;
  @Output() mapReadyAction = new EventEmitter<boolean>();

  private readonly MAP_TYPE_PREFIX = 'map-type:';
  private readonly COUNTIES_LAYER_ID = 'area-markers-counties';
  private readonly MUNICIPALITIES_LAYER_ID = 'area-markers-municipalities';
  private readonly LOCATIONS_LAYER_ID = 'area-markers-locations';
  private readonly LOCATION_POLYGONS_LAYER_ID = 'location-polygons';
  private readonly SELECTED_AREAS_OVERLAY_ID = 'area-overlay-selected';

  public map!: NbicMapComponent;
  private zoomControl?: ArtskartZoomControl;
  private fullscreenControl?: ArtskartFullscreenControl;
  private geolocationControl?: GeolocationMapControl;

  // Geometri-cache: persistent for hele sesjonen, tømmes aldri ved filterendring
  private geometryCacheByApiZoom = new Map<number, AreaMarkerDto[]>();
  // Antall-cache: nøkkel = `${zoomLevel}_${selectionKey}_${attrHash}` slik at hver
  // kombinasjon av områdevalg og attributtfiltre beholder sin ETag
  private countsCache = new Map<string, {
    counts: Map<string, number>;
    etag: string | null;
  }>();
  private mapReady = false;

  private readonly pendingAreaDataRequests = signal(0);
  readonly isLoadingAreaData = computed(() => this.pendingAreaDataRequests() > 0);

  private destroy$ = new Subject<void>();
  private cameraChanged$ = new Subject<void>();
  private fetchCounts$ = new Subject<{ requests: { dataZoomLevel: number; apiZoomLevel: number; visible: boolean }[]; extent: [number, number, number, number] }>();

  private readonly areasService = inject(AreasService);
  private readonly sharedMapService = inject(SharedMapService);
  private readonly logger = inject(LoggingService);
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly translate = inject(TranslateService);

  /**
   * Observasjonsattributtfiltre som påvirker antall per område.
   */
  private readonly attributeFilter = computed(
    () => {
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();
      const projectName = this.filterState.projectName().trim();
      const projectOrganizationId = this.filterState.projectOrganizationId();
      const collectionCode = this.filterState.collectionCode().trim();
      const catalogNumber = this.filterState.catalogNumber().trim();
      const withImages = imageFilterToWithImages(this.filterState.imageFilter());
      const periodMonths = this.filterState.selectedMonths();

      return {
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        registrationStatusId: this.filterState.selectedRegistrationStatusId() ?? undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        coordinatePrecisionFrom,
        coordinatePrecisionTo,
        periodFrom,
        periodTo,
        projectName: projectName || undefined,
        projectOrganizationId: projectOrganizationId ?? undefined,
        collectionCode: collectionCode || undefined,
        catalogNumber: catalogNumber || undefined,
        withImages,
        periodMonths: periodMonths.length ? periodMonths : undefined,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  /**
   * Komplett filter inkludert områdevalg.
   */
  private readonly locationFilter = computed<LocationSearchFilter>(
    () => {
      const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
      const attr = this.attributeFilter();
      return {
        ...attr,
        countyIds: countyIds.length ? countyIds : undefined,
        municipalityIds: municipalityIds.length ? municipalityIds : undefined,
        oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  private hasActiveAttributeFilters(): boolean {
    return Object.values(this.attributeFilter()).some(v =>
      v != null && (!Array.isArray(v) || v.length > 0),
    );
  }

  /**
   * Eneste effekt som reagerer på filterendringer.
   * Leser alle filtersignaler og trigget rebuildAllLayers ved endring.
   */
  private readonly _onFilterChange = effect(() => {
    this.locationFilter();
    if (this.mapReady) {
      this.rebuildAllLayers();
    }
  });

  ngAfterViewInit(): void {
    setTimeout(() => this.initializeMap(), MAP_CONFIG.initDelay);
  }

  private initializeMap(): void {
    try {
      if (!this.mapEl?.nativeElement) return;

      this.map = createMap(this.mapEl.nativeElement, {
        version: 1,
        id: MAP_CONFIG.mapId,
        projection: MAP_CONFIG.projection,
        center: MAP_CONFIG.center,
        zoom: ZoomConfig.DEFAULT_ZOOM_LEVEL,
        minZoom: MAP_CONFIG.minZoom,
        maxZoom: MAP_CONFIG.maxZoom,
        controls: {
          scaleLine: true,
          fullscreen: false,
          geolocation: true,
          zoom: false,
          attribution: true,
        },
      });

      this.setupBaseMapLayers();
      this.adoptMapControls();
      this.listenForLanguageChanges();
      this.map.on(MapEvents.Ready, () => this.onMapReady());
    } catch (error: unknown) {
      this.logger.error('Failed to initialize map:', 'MapComponent', error);
    }
  }

  private setupBaseMapLayers(): void {
    if (!this.map) return;
    this.map.addLayer(nbicMapPresets.osm);
    this.map.addLayer(nbicMapPresets.topografiskBaseLayer);
    this.map.addLayer(nbicMapPresets.topo4graatoneBaseLayer);
    this.map.addLayer(nbicMapPresets.svalbardBaseLayer);
    this.map.addLayer(nbicMapPresets.janmayenBaseLayer);

    const nib = {
      ...nbicMapPresets.nib,
      source: { ...nbicMapPresets.nib.source },
    };
    if (nib.source.type === 'wmts') {
      nib.source.options = {
        ...nib.source.options,
        tileLoadFunction: (tile: unknown, src: string) => {
          const token = this.sharedMapService.getNibToken();
          const separator = src.includes('?') ? '&' : '?';
          const img = (tile as ImageTile).getImage() as HTMLImageElement;
          img.src = token ? `${src}${separator}token=${token}` : src;
        },
      };
    }
    this.map.addLayer(nib);
  }

  private adoptMapControls(): void {
    this.adoptGeolocationControl();
    this.adoptFullscreenControl();
    this.adoptZoomControl();
    // TODO: Polygon draw controls should be adopted here using nbic-map-component's draw API
    // (map.startDrawing, map.stopDrawing, map.undoLastPoint, map.finishCurrent, etc.)
    // See Artsobservasjoner3's shared-map.component.ts for reference implementation.
  }

  private adoptZoomControl(): void {
    if (!this.map) return;
    this.zoomControl = new ArtskartZoomControl({
      zoomInTipLabel: this.translate.instant('mapToolbar.zoomInAriaLabel'),
      zoomOutTipLabel: this.translate.instant('mapToolbar.zoomOutAriaLabel'),
    });
    this.map.adoptControl(this.zoomControl, 'zoom');
  }

  private adoptFullscreenControl(): void {
    if (!this.map) return;
    // Fullscreen the container wrapping the map so the toolbar (map type selector etc.) stays visible
    const fullscreenSource = this.mapEl.nativeElement.parentElement ?? undefined;
    this.fullscreenControl = new ArtskartFullscreenControl(
      {
        tipLabel: this.translate.instant('mapToolbar.fullscreenAriaLabel'),
      },
      fullscreenSource,
    );
    this.map.adoptControl(this.fullscreenControl, 'fullscreen');
  }

  private adoptGeolocationControl(): void {
    if (!this.map) return;
    this.geolocationControl = createGeolocationControl(
      {
        tipLabel: this.translate.instant('mapToolbar.geolocationAriaLabel'),
        deniedTooltip: this.translate.instant('mapToolbar.geolocationDeniedTooltip'),
      },
      {
        onClick: () => this.map.zoomToGeolocation(14),
      },
    );
    this.map.adoptControl(this.geolocationControl, 'geolocation');
  }

  private listenForLanguageChanges(): void {
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.zoomControl?.updateLabels({
          zoomInTipLabel: this.translate.instant('mapToolbar.zoomInAriaLabel'),
          zoomOutTipLabel: this.translate.instant('mapToolbar.zoomOutAriaLabel'),
        });
        this.fullscreenControl?.updateLabels({
          tipLabel: this.translate.instant('mapToolbar.fullscreenAriaLabel'),
        });
        this.geolocationControl?.updateLabels({
          tipLabel: this.translate.instant('mapToolbar.geolocationAriaLabel'),
          deniedTooltip: this.translate.instant('mapToolbar.geolocationDeniedTooltip'),
        });
        this.rebuildAllLayers();
      });
  }

  private onMapReady(): void {
    this.mapReady = true;
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.setupAreaMarkerLayers();
    this.setupCountsFetchPipeline();
    this.setupLocationsFetchPipeline();
    this.setupCameraChangePipeline();
    this.prefetchAreaGeometries();
    this.rebuildAllLayers();
  }

  private setupAreaMarkerLayers(): void {
    this.map.addLayer({
      id: this.COUNTIES_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 50,
      zIndexPinned: true,
      maxZoom: ZoomConfig.ZOOM_COUNTIES_THRESHOLD,
    });

    this.map.addLayer({
      id: this.MUNICIPALITIES_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 50,
      zIndexPinned: true,
      minZoom: ZoomConfig.ZOOM_COUNTIES_THRESHOLD,
      maxZoom: ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD,
    });

    this.map.addLayer({
      id: this.SELECTED_AREAS_OVERLAY_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: false,
      zIndex: 60,
    });

    this.map.addLayer({
      id: this.LOCATIONS_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 100,
      zIndexPinned: true,
      minZoom: ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD,
      cluster: {
        enabled: true,
        distance: 50,
        keepSingleAsCluster: true,
        countField: 'observationCount',
        style: {
          type: 'simple',
          options: {
            circle: { radius: 17, fillColor: '#005B72', strokeColor: 'white', strokeWidth: 1 },
            text: { fillColor: 'white', font: 'bold 12px sans-serif' },
          },
        },
      },
    });

    this.map.addLayer({
      id: this.LOCATION_POLYGONS_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 90,
      zIndexPinned: true,
      minZoom: ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD,
    });
  }

  private setupCameraChangePipeline(): void {
    this.map.on(MapEvents.CameraChanged, () => this.cameraChanged$.next());
    this.cameraChanged$.pipe(
      debounceTime(150),
      takeUntil(this.destroy$),
    ).subscribe(() => this.rebuildAllLayers());
  }

  // ─── Unified rebuild ───────────────────────────────────────────────

  /**
   * Eneste inngangspunkt for å oppdatere kartlag.
   * Kalles ved filterendring, zoomendring og kamerabevegelse.
   */
  private rebuildAllLayers(): void {
    if (!this.map) return;

    const filter = this.locationFilter();
    const extent = this.map.getExtent() as [number, number, number, number];
    const olZoom = this.map.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    const apiZoomLevel = ZoomConfig.getApiZoomLevel(olZoom);

    // Oppdater overlay
    this.updateSelectedAreaOverlays();

    // Oppdater lokasjoner via debounced pipeline
    if (apiZoomLevel === ApiZoomLevel.LocationPoints) {
      this.emitLocationsFetch(extent, filter);
      return;
    }

    // Oppdater begge områdelag — synkront fra cache der mulig
    const pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[] = [];
    this.rebuildAreaLayer(ApiZoomLevel.Counties, filter, extent, pendingFetches);
    this.rebuildAreaLayer(ApiZoomLevel.Municipalities, filter, extent, pendingFetches);

    if (pendingFetches.length > 0) {
      // Prioriter synlig lag først, hent det andre i bakgrunnen etterpå
      const currentLayer = apiZoomLevel === ApiZoomLevel.Municipalities
        ? ApiZoomLevel.Municipalities : ApiZoomLevel.Counties;
      const sorted = [
        ...pendingFetches.filter(f => f.apiZoomLevel === currentLayer),
        ...pendingFetches.filter(f => f.apiZoomLevel !== currentLayer),
      ].map(f => ({ ...f, visible: f.apiZoomLevel === currentLayer }));
      this.fetchCounts$.next({ requests: sorted, extent });
    }
  }

  private loadFetchStart(): void {
    this.pendingAreaDataRequests.update(count => count + 1);
  }

  private loadFetchEnd(): void {
    this.pendingAreaDataRequests.update(count => Math.max(0, count - 1));
  }

  /**
   * Bygger et enkelt områdelag fra cache, eller legger til i pendingFetches.
   */
  private rebuildAreaLayer(
    apiZoomLevel: number,
    filter: LocationSearchFilter,
    extent: [number, number, number, number],
    pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[],
  ): void {
    const dataZoomLevel = apiZoomLevel === ApiZoomLevel.Counties && filter.municipalityIds?.length
      ? ApiZoomLevel.Municipalities
      : apiZoomLevel;
    const cachedGeometries = this.geometryCacheByApiZoom.get(dataZoomLevel);

    if (!cachedGeometries) {
      this.applyGeoJsonToLayer(apiZoomLevel, '{"type":"FeatureCollection","features":[]}');
      return;
    }

    if (!this.hasActiveAttributeFilters()) {
      const merged = this.mergeCountsIntoAreas(cachedGeometries, this.countsFromAreas(cachedGeometries), filter);
      const geojson = this.areasService.buildAreaGeoJson(merged, extent);
      this.applyGeoJsonToLayer(apiZoomLevel, geojson);
      return;
    }

    const cacheKey = this.countsCacheKey(dataZoomLevel, this.areaSelectionKey(filter));
    const cached = this.countsCache.get(cacheKey);
    if (cached) {
      const merged = this.mergeCountsIntoAreas(cachedGeometries, cached.counts, filter);
      const geojson = this.areasService.buildAreaGeoJson(merged, extent);
      this.applyGeoJsonToLayer(apiZoomLevel, geojson);
      return;
    }

    this.applyGeoJsonToLayer(apiZoomLevel, '{"type":"FeatureCollection","features":[]}');
    pendingFetches.push({ dataZoomLevel, apiZoomLevel });
  }

  // ─── Async pipelines ───────────────────────────────────────────────

  /**
   * Debounced pipeline for henting av antall fra backend.
   * Brukes kun når cache ikke dekker behovet.
   */
  private setupCountsFetchPipeline(): void {
    this.fetchCounts$.pipe(
      debounceTime(300),
      switchMap(({ requests, extent }) => {
        const filter = this.locationFilter();

        // Hent antall for alle forespurte zoomnivåer sekvensielt
        const fetches = requests.map(({ dataZoomLevel, apiZoomLevel, visible }) =>
          this.fetchCountsForZoomLevel(dataZoomLevel, apiZoomLevel, extent, filter, visible),
        );

        return rxConcat(...fetches);
      }),
      takeUntil(this.destroy$),
    ).subscribe();
  }

  private fetchCountsForZoomLevel(
    dataZoomLevel: number,
    apiZoomLevel: number,
    extent: [number, number, number, number],
    filter: LocationSearchFilter,
    visible: boolean,
  ): Observable<void> {
    const cachedGeometries = this.geometryCacheByApiZoom.get(dataZoomLevel);
    const selectionKey = this.areaSelectionKey(filter);

    if (cachedGeometries) {
      const cacheKey = this.countsCacheKey(dataZoomLevel, selectionKey);
      const existingCache = this.countsCache.get(cacheKey);

      // defer: loadFetchStart skal kun kjøre når requesten faktisk starter, ikke når den bygges/køes i concat
      return defer(() => {
        if (visible) this.loadFetchStart();
        return this.areasService.getAreaCounts(dataZoomLevel, filter, existingCache?.etag ?? undefined).pipe(
          tap(response => {
            if (!response.notModified && response.counts) {
              const countsMap = new Map(response.counts.map(c => [c.fid, c.observationCount]));
              this.countsCache.set(cacheKey, {
                counts: countsMap,
                etag: response.etag,
              });
            } else if (existingCache && response.etag) {
              existingCache.etag = response.etag;
            }
            const counts = this.countsCache.get(cacheKey)?.counts ?? new Map();
            const merged = this.mergeCountsIntoAreas(cachedGeometries, counts, filter);
            const geojson = this.areasService.buildAreaGeoJson(merged, extent);
            this.applyGeoJsonToLayer(apiZoomLevel, geojson);
          }),
          rxMap(() => undefined as void),
          catchError((err: unknown) => {
            this.logger.error(`Failed to load area counts for zoom level ${dataZoomLevel}:`, 'MapComponent', err);
            return EMPTY;
          }),
          finalize(() => { if (visible) this.loadFetchEnd(); }),
        );
      });
    }

    // Fallback: geometrier ikke i cache — hent alt
    const olZoom = dataZoomLevel === ApiZoomLevel.Municipalities
      ? ZoomConfig.ZOOM_COUNTIES_THRESHOLD
      : ZoomConfig.DEFAULT_ZOOM_LEVEL;

    return defer(() => {
      if (visible) this.loadFetchStart();
      return this.areasService.getAreaMarkers(olZoom, filter).pipe(
        tap(areas => {
          // Geometri-cachen tømmes aldri, så den må kun fylles med et komplett, ufiltrert sett
          if (selectionKey === this.EMPTY_SELECTION_KEY && !this.hasActiveAttributeFilters()) {
            this.geometryCacheByApiZoom.set(dataZoomLevel, areas);
          }
          const countsMap = this.countsFromAreas(areas);
          this.countsCache.set(this.countsCacheKey(dataZoomLevel, selectionKey), {
            counts: countsMap,
            etag: null,
          });
          const merged = this.mergeCountsIntoAreas(areas, countsMap, filter);
          const geojson = this.areasService.buildAreaGeoJson(merged, extent);
          this.applyGeoJsonToLayer(apiZoomLevel, geojson);
        }),
        rxMap(() => undefined as void),
        catchError((err: unknown) => {
          this.logger.error(`Failed to load area markers for zoom level ${dataZoomLevel}:`, 'MapComponent', err);
          return EMPTY;
        }),
        finalize(() => { if (visible) this.loadFetchEnd(); }),
      );
    });
  }

  private setupLocationsFetchPipeline(): void {
    this.locationsFetch$.pipe(
      debounceTime(300),
      switchMap(({ extent, filter }) => {
        this.loadFetchStart();
        const locations$ = this.areasService.getLocationsAsGeoJsonString(extent, filter).pipe(
          tap(geojson => this.applyGeoJsonToLayer(ApiZoomLevel.LocationPoints, geojson)),
          catchError((err: unknown) => {
            this.logger.error('Failed to load location points:', 'MapComponent', err);
            return EMPTY;
          }),
          finalize(() => this.loadFetchEnd()),
        );

        this.loadFetchStart();
        const polygons$ = this.areasService.getLocationPolygons(extent, filter).pipe(
          tap(geojson => this.map.updateGeoJSONLayer(this.LOCATION_POLYGONS_LAYER_ID, geojson, { mode: 'replace' })),
          catchError((err: unknown) => {
            this.logger.error('Failed to load location polygons:', 'MapComponent', err);
            return EMPTY;
          }),
          finalize(() => this.loadFetchEnd()),
        );

        return merge(locations$, polygons$);
      }),
      takeUntil(this.destroy$),
    ).subscribe();
  }

  private locationsFetch$ = new Subject<{ extent: [number, number, number, number]; filter: LocationSearchFilter }>();

  private emitLocationsFetch(extent: [number, number, number, number], filter: LocationSearchFilter): void {
    this.locationsFetch$.next({ extent, filter });
  }

  // ─── Prefetch ──────────────────────────────────────────────────────

  private prefetchAreaGeometries(): void {
    this.areasService.getAreaMarkers(ZoomConfig.DEFAULT_ZOOM_LEVEL).pipe(
      tap(areas => {
        this.seedCountsFromGeometries(ApiZoomLevel.Counties, areas);
        this.rebuildAllLayers();
      }),
      switchMap(() =>
        this.areasService.getAreaMarkers(ZoomConfig.ZOOM_COUNTIES_THRESHOLD).pipe(
          tap(areas => {
            this.seedCountsFromGeometries(ApiZoomLevel.Municipalities, areas);
            this.rebuildAllLayers();
          }),
        ),
      ),
      catchError((err: unknown) => {
        this.logger.error('Failed to prefetch area geometries:', 'MapComponent', err);
        return EMPTY;
      }),
      takeUntil(this.destroy$),
    ).subscribe();
  }

  /**
   * Lagrer prefetchede geometrier, og antallene som følger med dem.
   * Antallene fra prefetch er ufiltrerte, så de brukes kun når ingen attributtfiltre
   * er aktive i det svaret kommer inn — ellers hentes riktige antall via fetchCounts$.
   */
  private seedCountsFromGeometries(apiZoomLevel: number, areas: AreaMarkerDto[]): void {
    this.geometryCacheByApiZoom.set(apiZoomLevel, areas);

    if (this.hasActiveAttributeFilters()) return;

    this.countsCache.set(this.countsCacheKey(apiZoomLevel, this.EMPTY_SELECTION_KEY), {
      counts: this.countsFromAreas(areas),
      etag: null,
    });
  }

  // ─── Helpers ───────────────────────────────────────────────────────

  private readonly EMPTY_SELECTION_KEY = JSON.stringify([[], [], []]);

  /**
   * Nøkkel som identifiserer hvilket områdevalg et sett med antall gjelder for.
   * Brukes til å avgjøre om cachede antall fortsatt er gyldige.
   */
  private areaSelectionKey(filter: LocationSearchFilter): string {
    return JSON.stringify([
      [...(filter.countyIds ?? [])].sort(),
      [...(filter.municipalityIds ?? [])].sort(),
      [...(filter.oceanAreaIds ?? [])].sort(),
    ]);
  }

  private countsCacheKey(zoomLevel: number, selectionKey: string): string {
    return `${zoomLevel}_${selectionKey}_${JSON.stringify(this.attributeFilter())}`;
  }

  private filterCachedAreasBySelection(areas: AreaMarkerDto[], filter: LocationSearchFilter): AreaMarkerDto[] {
    const countyFids = new Set(filter.countyIds ?? []);
    const municipalityFids = new Set(filter.municipalityIds ?? []);
    const oceanAreaFids = new Set(filter.oceanAreaIds ?? []);

    if (countyFids.size === 0 && municipalityFids.size === 0 && oceanAreaFids.size === 0) {
      return areas;
    }

    return areas.filter(a =>
      countyFids.has(a.fid) ||
      municipalityFids.has(a.fid) ||
      oceanAreaFids.has(a.fid) ||
      (a.parentFid && countyFids.has(a.parentFid)),
    );
  }

  /**
   * Slår sammen geometrier og antall, og avgjør hvilke områder som skal tegnes.
   *
   * Regel: et område vises når det har treff, eller når det er eksplisitt valgt av brukeren.
   * Valgte områder uten treff vises med "0" slik at brukeren ser at valget ga null resultater.
   */
  private mergeCountsIntoAreas(areas: AreaMarkerDto[], counts: Map<string, number>, filter: LocationSearchFilter): AreaMarkerDto[] {
    const selectedFids = new Set([
      ...(filter.countyIds ?? []),
      ...(filter.municipalityIds ?? []),
      ...(filter.oceanAreaIds ?? []),
    ]);

    return this.filterCachedAreasBySelection(areas, filter)
      .map(a => ({ ...a, observationCount: counts.get(a.fid) ?? 0 }))
      .filter(a => (a.observationCount ?? 0) > 0 || selectedFids.has(a.fid));
  }

  /**
   * Bruker de forhåndsberegnede antallene som følger med geometriene.
   */
  private countsFromAreas(areas: AreaMarkerDto[]): Map<string, number> {
    return new Map(areas.map(a => [a.fid, a.observationCount ?? 0]));
  }

  private updateSelectedAreaOverlays(): void {
    if (!this.map) return;

    const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
    const selectedOceanAreaFids = this.filterState.selectedOceanAreaIds();

    const countyAreas = this.geometryCacheByApiZoom.get(ApiZoomLevel.Counties) ?? [];
    const municipalityAreas = this.geometryCacheByApiZoom.get(ApiZoomLevel.Municipalities) ?? [];

    const parentCountyFids = municipalityIds.length > 0
      ? [...new Set(
          municipalityAreas
            .filter(a => municipalityIds.includes(a.fid) && a.parentFid)
            .map(a => a.parentFid),
        )]
      : [];

    const countyLevelFids = [...new Set([...countyIds, ...selectedOceanAreaFids, ...parentCountyFids])];
    const countyFeatures = this.areasService.buildOverlayFeatures(countyAreas, countyLevelFids);
    const municipalityFeatures = this.areasService.buildOverlayFeatures(municipalityAreas, municipalityIds);
    const combined = JSON.stringify({
      type: 'FeatureCollection',
      features: [...countyFeatures, ...municipalityFeatures],
    });

    this.map.updateGeoJSONLayer(this.SELECTED_AREAS_OVERLAY_ID, combined, { mode: 'replace' });
  }

  private applyGeoJsonToLayer(apiZoomLevel: number, geojson: string): void {
    if (!this.map) return;

    const isLocationPoints = apiZoomLevel === ApiZoomLevel.LocationPoints;
    const layerId = isLocationPoints
      ? this.LOCATIONS_LAYER_ID
      : apiZoomLevel >= ApiZoomLevel.Municipalities
        ? this.MUNICIPALITIES_LAYER_ID
        : this.COUNTIES_LAYER_ID;

    this.map.updateGeoJSONLayer(layerId, geojson, {
      mode: 'replace',
      ...(isLocationPoints && { dataProjection: 'EPSG:4326' }),
    });
  }

  onIconClick(iconName: string): void {
    if (!iconName.startsWith(this.MAP_TYPE_PREFIX)) {
      return;
    }
    const layerId = iconName.slice(this.MAP_TYPE_PREFIX.length);
    if (!this.map || !layerId) return;
    this.map.setLayerVisibility(layerId, true);
  }

  private cleanup(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.geolocationControl?.dispose();
    this.geometryCacheByApiZoom.clear();
    this.countsCache.clear();
    this.map?.destroy?.();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }
}
