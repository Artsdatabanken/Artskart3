import { createMap, MapEvents, MapEventPayload, NbicMapComponent, nbicMapPresets } from '@artsdatabanken/nbic-map-component';
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
  untracked,
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Observable, Subject, EMPTY, merge, concat as rxConcat, defer } from 'rxjs';
import { catchError, debounceTime, map as rxMap, finalize, switchMap, takeUntil, tap } from 'rxjs/operators';
import { AreasService, LocationSearchFilter } from '@core/services/areas/areas.service';
import { LocationCountResult } from '@shared/types/api.types';
import { AreaMarkerDto } from '@shared/types/api.types';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { CommonModule } from '@angular/common';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { Feature, ImageTile } from 'ol';
import type OlMap from 'ol/Map';
import type { Pixel } from 'ol/pixel';
import Point from 'ol/geom/Point';
import { unByKey } from 'ol/Observable';
import type { EventsKey } from 'ol/events';
import { ApiZoomLevel, LocationFeatureProperties, PointerClickFeature } from './map.types';
import { FilterStateService, imageFilterToWithImages } from '../../services/filter-state/filter-state.service';
import { AreaService } from '../../services/area/area.service';
import { ArtskartZoomControl } from './controls/zoom.control';
import { ArtskartFullscreenControl } from './controls/fullscreen.control';
import { createGeolocationControl, GeolocationMapControl } from './controls/geolocation.control';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationListComponent } from '@shared/components/observation-list.component/observation-list.component';
import { LoadingIndicatorComponent } from '../loading-indicator/loading-indicator.component';
import { ObservationListInfoDto } from '@shared/types/api.types';
import {SearchFilterService} from '@shared/services/search-filter/search-filter.service';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MapToolbarComponent, ObservationListComponent, LoadingIndicatorComponent, TranslateModule],
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
  private countsCache = new Map<
    string,
    {
      counts: Map<string, number>;
      etag: string | null;
    }
  >();
  private mapReady = false;
  private lastValidExtent: [number, number, number, number] | null = null;
  private mapVisible = true;
  private visibilityObserver?: ResizeObserver;

  private readonly pendingAreaDataRequests = signal(0);
  readonly isLoadingAreaData = computed(() => this.pendingAreaDataRequests() > 0);

  private fetchGeneration = 0;

  private destroy$ = new Subject<void>();
  private cameraChanged$ = new Subject<void>();
  private fetchCounts$ = new Subject<{
    requests: { dataZoomLevel: number; apiZoomLevel: number; visible: boolean }[];
    extent: [number, number, number, number];
  }>();
  private locationClick$ = new Subject<number[]>();
  private locationCountFetch$ = new Subject<LocationSearchFilter>();

  private readonly areasService = inject(AreasService);
  private readonly sharedMapService = inject(SharedMapService);
  private readonly observationService = inject(ObservationService);
  private readonly logger = inject(LoggingService);
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly translate = inject(TranslateService);
  private readonly searchFilterService = inject(SearchFilterService);

  /**
   * Observasjonsattributtfiltre som påvirker antall per område.
   */
  private readonly attributeFilter = computed(
    () => {
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();
      const datasetOrgId = this.filterState.datasetOrgId();
      const projectOrgId = this.filterState.projectOrgId();
      const catalogObservationIds = this.filterState.catalogObservationIds();
      const withImages = imageFilterToWithImages(this.filterState.imageFilter());
      const periodMonths = this.filterState.selectedMonths();

      return {
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        registrationStatusId: this.filterState.selectedRegistrationStatusId() ?? undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        taxonIds: this.filterState.selectedTaxonIds().length ? this.filterState.selectedTaxonIds() : undefined,
        coordinatePrecisionFrom,
        coordinatePrecisionTo,
        periodFrom,
        periodTo,
        datasetOrgId: datasetOrgId ?? undefined,
        projectOrgId: projectOrgId ?? undefined,
        observationIds: catalogObservationIds.length ? catalogObservationIds : undefined,
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
    return Object.values(this.attributeFilter()).some((v) => v != null && (!Array.isArray(v) || v.length > 0));
  }

  /**
   * Eneste effekt som reagerer på filterendringer.
   * Leser alle filtersignaler og trigget rebuildAllLayers ved endring.
   */
  private readonly _onFilterChange = effect(() => {
    const filter = this.locationFilter();
    // Untracked for å ungå evig løkke mens tellingen pågår
    untracked(() => {
      if (this.mapReady) {
        this.locationCountPending = true;
        this.locationCountFetch$.next(filter);
        this.rebuildAllLayers();
      }
    });
  });

  public showObservationList = signal(false);
  public observationList = signal<ObservationListInfoDto[]>([]);
  public clickCoordinates = signal<number[]>([]);

  private readonly locationCountResult = signal<LocationCountResult | null>(null);

  private readonly directClusterMode = computed(() => {
    const result = this.locationCountResult();
    return result != null && !result.truncated && result.count <= ZoomConfig.DIRECT_CLUSTER_MAX_LOCATIONS;
  });

  private locationCountPending = false;

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
      this.map.on(MapEvents.PointerClick, (payload) => {
        this.handleLocationClick(payload);
        this.handleAreaMarkerClick(payload.features as PointerClickFeature[] | null);
      });
      this.locationClick$
        .pipe(
          switchMap((ids) =>
            this.observationService.getObservationByLocation(ids, this.searchFilterService.observationFilter()).pipe(
              catchError((err: unknown) => {
                this.logger.error('Failed to fetch observations for locations', ids.toString(), err);
                this.showObservationList.set(false);
                return EMPTY;
              }),
            ),
          ),
          takeUntil(this.destroy$),
        )
        .subscribe((observations) => {
          this.observationList.set(observations);
          this.showObservationList.set(true);
        });
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
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
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
    });
  }

  private onMapReady(): void {
    this.mapReady = true;
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.setupAreaMarkerLayers();
    this.setupMarkerCursor();
    this.setupCountsFetchPipeline();
    this.setupLocationsFetchPipeline();
    this.setupLocationCountPipeline();
    this.setupCameraChangePipeline();
    this.setupVisibilityObserver();
    this.prefetchAreaGeometries();
    this.locationCountFetch$.next(this.locationFilter());
    this.rebuildAllLayers();
  }

  private readonly CLICK_ANIMATION_DURATION_MS = 300;

  private handleAreaMarkerClick(features: PointerClickFeature[] | null): void {
    if (!features?.length) return;

    let hitMarkerLayer: string | null = null;
    for (const hit of features) {
      const targetZoom = this.zoomTargetForLayer(hit.layerId);
      if (targetZoom == null) continue;
      hitMarkerLayer = hit.layerId;

      const centroid = this.extractCentroid(hit.properties);
      if (!centroid) continue; // polygon outline feature in the same layer has no centroid

      this.zoomToCentroid(centroid, targetZoom);
      return;
    }

    if (hitMarkerLayer) {
      this.logger.warn(`Marker in layer ${hitMarkerLayer} has no valid centroid; ignoring click`, 'MapComponent');
    }
  }

  private zoomTargetForLayer(layerId: string): number | null {
    if (layerId === this.COUNTIES_LAYER_ID) return ZoomConfig.ZOOM_AFTER_COUNTY_CLICK;
    if (layerId === this.MUNICIPALITIES_LAYER_ID) return ZoomConfig.ZOOM_AFTER_MUNICIPALITY_CLICK;
    return null;
  }

  private extractCentroid(properties?: Record<string, unknown>): [number, number] | null {
    const centroid = properties?.['centroid'] as { x?: unknown; y?: unknown } | undefined;
    if (typeof centroid?.x !== 'number' || typeof centroid?.y !== 'number') return null;
    if (!Number.isFinite(centroid.x) || !Number.isFinite(centroid.y)) return null;
    return [centroid.x, centroid.y];
  }

  private zoomToCentroid(centroid: [number, number], zoom: number): void {
    // nbic-map-component exposes no fly-to; use the OL view via the adopted zoom control.
    const view = this.zoomControl?.getMap()?.getView();
    if (view) {
      view.animate({ center: centroid, zoom, duration: this.CLICK_ANIMATION_DURATION_MS });
      return;
    }
    this.map?.setCenter(centroid);
    this.map?.setZoom(zoom);
  }

  private zoomToClusterMembers(members: Feature<Point>[]): boolean {
    const view = this.zoomControl?.getMap()?.getView();
    if (!view) return false;

    const currentZoom = view.getZoom() ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    if (this.isAtMaxZoom(currentZoom)) return false;

    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const member of members) {
      const coordinate = member.getGeometry()?.getCoordinates();
      if (!coordinate) continue;
      minX = Math.min(minX, coordinate[0]);
      minY = Math.min(minY, coordinate[1]);
      maxX = Math.max(maxX, coordinate[0]);
      maxY = Math.max(maxY, coordinate[1]);
    }
    if (!Number.isFinite(minX)) return false;

    if (minX === maxX && minY === maxY) {
      view.animate({
        center: [minX, minY],
        zoom: Math.min(currentZoom + ZoomConfig.CLUSTER_CLICK_ZOOM_STEP, MAP_CONFIG.maxZoom),
        duration: this.CLICK_ANIMATION_DURATION_MS,
      });
    } else {
      view.fit([minX, minY, maxX, maxY], {
        padding: [80, 80, 80, 80],
        duration: this.CLICK_ANIMATION_DURATION_MS,
        maxZoom: MAP_CONFIG.maxZoom,
      });
    }
    return true;
  }

  private isAtMaxZoom(zoom?: number): boolean {
    const currentZoom = zoom ?? this.zoomControl?.getMap()?.getView().getZoom() ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    return currentZoom >= MAP_CONFIG.maxZoom - 0.01;
  }

  private setupMarkerCursor(): void {
    const olMap = this.zoomControl?.getMap();
    if (!olMap) return;

    this.markerCursorKey = olMap.on('pointermove', (evt) => {
      const cursor = this.resolveCursorAtPixel(olMap, evt.pixel);
      const target = olMap.getTargetElement();
      if (cursor) {
        target.style.cursor = cursor;
        this.markerCursorActive = true;
      } else if (this.markerCursorActive) {
        target.style.cursor = '';
        this.markerCursorActive = false;
      }
    });
  }

  private resolveCursorAtPixel(olMap: OlMap, pixel: Pixel): string {
    let pointerHit = false;
    const zoomCursor = olMap.forEachFeatureAtPixel(
      pixel,
      (feature, layer): string | undefined => {
        const layerId = layer?.get('id') as string | undefined;
        if (layerId === this.LOCATIONS_LAYER_ID) {
          const members = feature?.get('features') as Feature<Point>[] | undefined;
          if ((members?.length ?? 1) > ZoomConfig.CLUSTER_CLICK_MAX_LOCATIONS && !this.isAtMaxZoom()) {
            return 'zoom-in';
          }
          pointerHit = true;
          return undefined;
        }
        if (layerId === this.LOCATION_POLYGONS_LAYER_ID) {
          pointerHit = true;
          return undefined;
        }

        if (feature?.get('centroid') != null) return 'zoom-in';
        return undefined;
      },
      {
        hitTolerance: 5,
        layerFilter: (layer) => {
          const id = layer?.get('id') as string | undefined;
          return (
            id === this.COUNTIES_LAYER_ID ||
            id === this.MUNICIPALITIES_LAYER_ID ||
            id === this.LOCATIONS_LAYER_ID ||
            id === this.LOCATION_POLYGONS_LAYER_ID
          );
        },
      },
    );
    return zoomCursor ?? (pointerHit ? 'pointer' : '');
  }

  private markerCursorKey?: EventsKey;
  private markerCursorActive = false;

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

    // Synlighet styres manuelt i rebuildWithExtent (ikke minZoom) slik at
    // direkte klustermodus kan vise laget på alle zoomnivåer.
    this.map.addLayer({
      id: this.LOCATIONS_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 100,
      zIndexPinned: true,
      cluster: {
        enabled: true,
        distance: 40,
        keepSingleAsCluster: false,
        countField: 'observationCount',
        style: {
          type: 'simple',
          options: {
            circle: { radius: 14, fillColor: '#005A71', strokeColor: '#D2DDE0', strokeWidth: 1.5 },
            text: { fillColor: 'white', font: '10px Chivo, system-ui, sans-serif' },
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
    });
  }

  private setupCameraChangePipeline(): void {
    this.map.on(MapEvents.CameraChanged, () => this.cameraChanged$.next());
    this.cameraChanged$.pipe(debounceTime(150), takeUntil(this.destroy$)).subscribe(() => this.rebuildAllLayers());
  }

  // ─── Unified rebuild ───────────────────────────────────────────────

  /**
   * Eneste inngangspunkt for å oppdatere kartlag.
   * Kalles ved filterendring, zoomendring og kamerabevegelse.
   *
   * When the map is in the DOM but not visible (0×0), getExtent() is not
   * reliable (OpenLayers falls back to a default viewport size). In that case
   * data is fetched against the last known extent so the cache is warm when
   * the map becomes visible again, while rendering is deferred (see applyGeoJsonToLayer).
   */
  private rebuildAllLayers(): void {
    if (!this.map) return;

    const filter = this.locationFilter();

    if (!this.mapVisible) {
      // Location points are not cached locally — defer fetching them until the map is visible.
      // Zoom is independent of container size and safe to read.
      const olZoom = this.map.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
      const needsLocations = this.directClusterMode() || ZoomConfig.getApiZoomLevel(olZoom) === ApiZoomLevel.LocationPoints;
      if (!needsLocations && this.lastValidExtent) {
        this.rebuildWithExtent(filter, this.lastValidExtent);
      }
      return;
    }

    const extent = this.readValidExtent();
    if (!extent) return;
    this.lastValidExtent = extent;
    this.rebuildWithExtent(filter, extent);
  }

  private readValidExtent(): [number, number, number, number] | null {
    // A 0×0 container means getExtent() falls back to a default viewport size
    // and returns a bogus extent. Checked here (not only via mapVisible) so the
    // initial rebuild is also guarded before the first ResizeObserver callback.
    const rect = this.mapEl?.nativeElement.getBoundingClientRect();
    if (!rect || rect.width === 0 || rect.height === 0) return null;

    const extent = this.map.getExtent() as number[] | undefined;
    if (!extent || extent.length < 4 || !extent.every(Number.isFinite)) return null;
    if (extent[0] === extent[2] || extent[1] === extent[3]) return null;
    return extent as unknown as [number, number, number, number];
  }

  /**
   * Observes the map container's size. When the map transitions from hidden
   * (0×0) to visible, a rebuild is triggered — deferred to the next frame so
   * OpenLayers' own ResizeObserver has run updateSize() before the extent is read.
   *
   * mapVisible is only updated from observer callbacks: a real ResizeObserver
   * reports the initial size immediately upon observe(), so no synchronous
   * initialization is needed (and it keeps tests deterministic, where
   * ResizeObserver is stubbed).
   */
  private setupVisibilityObserver(): void {
    const el = this.mapEl?.nativeElement;
    if (!el || typeof ResizeObserver === 'undefined') return;

    this.visibilityObserver = new ResizeObserver((entries) => {
      const { width, height } = entries[0].contentRect;
      const visible = width > 0 && height > 0;
      if (visible === this.mapVisible) return;
      this.mapVisible = visible;
      if (visible) {
        requestAnimationFrame(() => this.cameraChanged$.next());
      }
    });
    this.visibilityObserver.observe(el);
  }

  private rebuildWithExtent(filter: LocationSearchFilter, extent: [number, number, number, number]): void {
    this.fetchGeneration++;
    const olZoom = this.map.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    const apiZoomLevel = ZoomConfig.getApiZoomLevel(olZoom);
    const direct = this.directClusterMode();
    const showLocations = direct || apiZoomLevel === ApiZoomLevel.LocationPoints;

    // Lagene har ingen minZoom — synlighet styres her. Fylke-/kommunelagene
    // har egne zoomterskler som begrenser dem når de er satt synlige.
    this.map.setLayerVisibility(this.LOCATIONS_LAYER_ID, showLocations);
    this.map.setLayerVisibility(this.LOCATION_POLYGONS_LAYER_ID, showLocations);
    this.map.setLayerVisibility(this.COUNTIES_LAYER_ID, !direct);
    this.map.setLayerVisibility(this.MUNICIPALITIES_LAYER_ID, !direct);

    // Oppdater overlay
    this.updateSelectedAreaOverlays();

    if (showLocations) {
      if (!direct || !this.locationCountPending) {
        this.fetchLocationsIfNeeded(extent, filter);
      }
      return;
    }

    // Oppdater begge områdelag — synkront fra cache der mulig
    const pendingFetches: { dataZoomLevel: number; apiZoomLevel: number }[] = [];
    this.rebuildAreaLayer(ApiZoomLevel.Counties, filter, extent, pendingFetches);
    this.rebuildAreaLayer(ApiZoomLevel.Municipalities, filter, extent, pendingFetches);

    if (pendingFetches.length > 0) {
      // Prioriter synlig lag først, hent det andre i bakgrunnen etterpå
      const currentLayer = apiZoomLevel === ApiZoomLevel.Municipalities ? ApiZoomLevel.Municipalities : ApiZoomLevel.Counties;
      const sorted = [
        ...pendingFetches.filter((f) => f.apiZoomLevel === currentLayer),
        ...pendingFetches.filter((f) => f.apiZoomLevel !== currentLayer),
      ].map((f) => ({ ...f, visible: f.apiZoomLevel === currentLayer }));
      this.fetchCounts$.next({ requests: sorted, extent });
    }
  }

  private loadFetchStart(): void {
    this.pendingAreaDataRequests.update((count) => count + 1);
  }

  private loadFetchEnd(): void {
    this.pendingAreaDataRequests.update((count) => Math.max(0, count - 1));
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
    const dataZoomLevel =
      apiZoomLevel === ApiZoomLevel.Counties && filter.municipalityIds?.length ? ApiZoomLevel.Municipalities : apiZoomLevel;
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

  private handleLocationClick(payload: MapEventPayload<typeof MapEvents.PointerClick>): void {
    const features = payload.features as PointerClickFeature[];
    if (!features) {
      this.showObservationList.set(false);
      return;
    }

    const hasRelevantFeature = features.some(
      ({ layerId }) => layerId === this.LOCATIONS_LAYER_ID || layerId === this.LOCATION_POLYGONS_LAYER_ID,
    );

    if (!hasRelevantFeature) {
      this.showObservationList.set(false);
      return;
    }

    this.clickCoordinates.set(payload.clickCoordinate.map((coordinate) => Math.round(coordinate)));

    const memberGroups = features
      .filter(({ layerId }) => layerId === this.LOCATIONS_LAYER_ID)
      .map(({ feature }) => (feature.get('features') as Feature<Point>[] | undefined) ?? [feature as Feature<Point>]);

    const largeCluster = memberGroups.find((members) => members.length > ZoomConfig.CLUSTER_CLICK_MAX_LOCATIONS);
    if (largeCluster && this.zoomToClusterMembers(largeCluster)) {
      this.showObservationList.set(false);
      return;
    }

    const locationIds = memberGroups
      .flatMap((members) => members)
      .map((member) => (member.getProperties() as LocationFeatureProperties).id);

    const polygonLocationIds = features
      .filter(({ layerId }) => layerId === this.LOCATION_POLYGONS_LAYER_ID)
      .map(({ feature }) => (feature.getProperties() as LocationFeatureProperties).id);

    const ids = [...locationIds, ...polygonLocationIds].filter((item): item is number => item !== undefined);
    this.locationClick$.next(ids);
  }

  // ─── Async pipelines ───────────────────────────────────────────────

  /**
   * Debounced pipeline for henting av antall fra backend.
   * Brukes kun når cache ikke dekker behovet.
   */
  private setupCountsFetchPipeline(): void {
    this.fetchCounts$
      .pipe(
        switchMap(({ requests, extent }) => {
          const filter = this.locationFilter();

          // Hent antall for alle forespurte zoomnivåer sekvensielt
          const fetches = requests.map(({ dataZoomLevel, apiZoomLevel, visible }) =>
            this.fetchCountsForZoomLevel(dataZoomLevel, apiZoomLevel, extent, filter, visible),
          );

          return rxConcat(...fetches);
        }),
        takeUntil(this.destroy$),
      )
      .subscribe();
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
    const generation = this.fetchGeneration;
    const requestHadAttributeFilters = this.hasActiveAttributeFilters();

    if (cachedGeometries) {
      const cacheKey = this.countsCacheKey(dataZoomLevel, selectionKey);
      const existingCache = this.countsCache.get(cacheKey);

      // defer: loadFetchStart skal kun kjøre når requesten faktisk starter, ikke når den bygges/køes i concat
      return defer(() => {
        if (visible) this.loadFetchStart();
        return this.areasService.getAreaCounts(dataZoomLevel, filter, existingCache?.etag ?? undefined).pipe(
          tap((response) => {
            if (!response.notModified && response.counts) {
              const countsMap = new Map(response.counts.map((c) => [c.fid, c.observationCount]));
              this.countsCache.set(cacheKey, {
                counts: countsMap,
                etag: response.etag,
              });
            } else if (existingCache && response.etag) {
              existingCache.etag = response.etag;
            }
            if (generation !== this.fetchGeneration) return;
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
          finalize(() => {
            if (visible) this.loadFetchEnd();
          }),
        );
      });
    }

    // Fallback: geometrier ikke i cache — hent alt
    const olZoom = dataZoomLevel === ApiZoomLevel.Municipalities ? ZoomConfig.ZOOM_COUNTIES_THRESHOLD : ZoomConfig.DEFAULT_ZOOM_LEVEL;

    return defer(() => {
      if (visible) this.loadFetchStart();
      return this.areasService.getAreaMarkers(olZoom, filter).pipe(
        tap((areas) => {
          if (selectionKey === this.EMPTY_SELECTION_KEY && !requestHadAttributeFilters) {
            this.geometryCacheByApiZoom.set(dataZoomLevel, areas);
          }
          const countsMap = this.countsFromAreas(areas);
          this.countsCache.set(this.countsCacheKey(dataZoomLevel, selectionKey), {
            counts: countsMap,
            etag: null,
          });
          if (generation !== this.fetchGeneration) return;
          const merged = this.mergeCountsIntoAreas(areas, countsMap, filter);
          const geojson = this.areasService.buildAreaGeoJson(merged, extent);
          this.applyGeoJsonToLayer(apiZoomLevel, geojson);
        }),
        rxMap(() => undefined as void),
        catchError((err: unknown) => {
          this.logger.error(`Failed to load area markers for zoom level ${dataZoomLevel}:`, 'MapComponent', err);
          return EMPTY;
        }),
        finalize(() => {
          if (visible) this.loadFetchEnd();
        }),
      );
    });
  }

  private setupLocationsFetchPipeline(): void {
    this.locationsFetch$
      .pipe(
        debounceTime(300),
        switchMap(({ extent, filter }) => {
          const generation = this.fetchGeneration;
          this.loadFetchStart();
          const locations$ = this.areasService.getLocationsAsGeoJsonString(extent, filter).pipe(
            tap((geojson) => {
              if (generation === this.fetchGeneration) {
                this.applyGeoJsonToLayer(ApiZoomLevel.LocationPoints, geojson);
                this.lastLocationsCoverage = { filterKey: JSON.stringify(filter), extent };
              }
            }),
            catchError((err: unknown) => {
              this.logger.error('Failed to load location points:', 'MapComponent', err);
              return EMPTY;
            }),
            finalize(() => this.loadFetchEnd()),
          );

          this.loadFetchStart();
          const polygons$ = this.areasService.getLocationPolygons(extent, filter).pipe(
            tap((geojson) => {
              if (this.mapVisible && generation === this.fetchGeneration) {
                this.map.updateGeoJSONLayer(this.LOCATION_POLYGONS_LAYER_ID, geojson, { mode: 'replace' });
              }
            }),
            catchError((err: unknown) => {
              this.logger.error('Failed to load location polygons:', 'MapComponent', err);
              return EMPTY;
            }),
            finalize(() => this.loadFetchEnd()),
          );

          return merge(locations$, polygons$);
        }),
        takeUntil(this.destroy$),
      )
      .subscribe();
  }

  private locationsFetch$ = new Subject<{ extent: [number, number, number, number]; filter: LocationSearchFilter }>();

  private lastLocationsCoverage: { filterKey: string; extent: [number, number, number, number] } | null = null;

  private emitLocationsFetch(extent: [number, number, number, number], filter: LocationSearchFilter): void {
    this.locationsFetch$.next({ extent, filter });
  }

  private fetchLocationsIfNeeded(extent: [number, number, number, number], filter: LocationSearchFilter): void {
    const coverage = this.lastLocationsCoverage;
    if (
      coverage &&
      coverage.filterKey === JSON.stringify(filter) &&
      extent[0] >= coverage.extent[0] &&
      extent[1] >= coverage.extent[1] &&
      extent[2] <= coverage.extent[2] &&
      extent[3] <= coverage.extent[3]
    ) {
      return;
    }
    this.emitLocationsFetch(extent, filter);
  }

  private setupLocationCountPipeline(): void {
    this.locationCountFetch$
      .pipe(
        debounceTime(200),
        switchMap((filter) =>
          this.areasService.getLocationCount(filter).pipe(
            rxMap((result) => ({ filter, result })),
            catchError((err: unknown) => {
              this.logger.error('Failed to fetch location count:', 'MapComponent', err);
              if (JSON.stringify(filter) === JSON.stringify(this.locationFilter())) {
                this.locationCountPending = false;
                this.rebuildAllLayers();
              }
              return EMPTY;
            }),
          ),
        ),
        takeUntil(this.destroy$),
      )
      .subscribe(({ filter, result }) => {
        if (JSON.stringify(filter) !== JSON.stringify(this.locationFilter())) return;
        this.locationCountPending = false;
        this.locationCountResult.set(result);
        this.rebuildAllLayers();
      });
  }

  // ─── Prefetch ──────────────────────────────────────────────────────

  private prefetchAreaGeometries(): void {
    this.areasService
      .getAreaMarkers(ZoomConfig.DEFAULT_ZOOM_LEVEL)
      .pipe(
        tap((areas) => {
          this.seedCountsFromGeometries(ApiZoomLevel.Counties, areas);
          this.rebuildAllLayers();
        }),
        switchMap(() =>
          this.areasService.getAreaMarkers(ZoomConfig.ZOOM_COUNTIES_THRESHOLD).pipe(
            tap((areas) => {
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
      )
      .subscribe();
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

    return areas.filter(
      (a) =>
        a.fid != null &&
        (countyFids.has(a.fid) ||
          municipalityFids.has(a.fid) ||
          oceanAreaFids.has(a.fid) ||
          (a.parentFid != null && countyFids.has(a.parentFid))),
    );
  }

  /**
   * Slår sammen geometrier og antall, og avgjør hvilke områder som skal tegnes.
   *
   * Regel: et område vises når det har treff, eller når det er eksplisitt valgt av brukeren.
   * Valgte områder uten treff vises med "0" slik at brukeren ser at valget ga null resultater.
   */
  private mergeCountsIntoAreas(areas: AreaMarkerDto[], counts: Map<string, number>, filter: LocationSearchFilter): AreaMarkerDto[] {
    const selectedFids = new Set([...(filter.countyIds ?? []), ...(filter.municipalityIds ?? []), ...(filter.oceanAreaIds ?? [])]);

    return this.filterCachedAreasBySelection(areas, filter)
      .map((a) => ({ ...a, observationCount: (a.fid != null ? counts.get(a.fid) : undefined) ?? 0 }))
      .filter((a) => (a.observationCount ?? 0) > 0 || (a.fid != null && selectedFids.has(a.fid)));
  }

  /**
   * Bruker de forhåndsberegnede antallene som følger med geometriene.
   */
  private countsFromAreas(areas: AreaMarkerDto[]): Map<string, number> {
    const counts = new Map<string, number>();
    for (const a of areas) {
      if (a.fid != null) counts.set(a.fid, a.observationCount ?? 0);
    }
    return counts;
  }

  private updateSelectedAreaOverlays(): void {
    if (!this.map || !this.mapVisible) return;

    const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
    const selectedOceanAreaFids = this.filterState.selectedOceanAreaIds();

    const countyAreas = this.geometryCacheByApiZoom.get(ApiZoomLevel.Counties) ?? [];
    const municipalityAreas = this.geometryCacheByApiZoom.get(ApiZoomLevel.Municipalities) ?? [];

    const parentCountyFids =
      municipalityIds.length > 0
        ? [
            ...new Set(
              municipalityAreas
                .filter((a) => a.fid != null && municipalityIds.includes(a.fid))
                .map((a) => a.parentFid)
                .filter((fid): fid is string => fid != null),
            ),
          ]
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
    // Don't render while the map is hidden — layers are rebuilt from cache
    // when the map becomes visible again (see setupVisibilityObserver).
    if (!this.map || !this.mapVisible) return;

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
    this.visibilityObserver?.disconnect();
    if (this.markerCursorKey) unByKey(this.markerCursorKey);
    this.geolocationControl?.dispose();
    this.geometryCacheByApiZoom.clear();
    this.countsCache.clear();
    this.map?.destroy?.();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }
}
