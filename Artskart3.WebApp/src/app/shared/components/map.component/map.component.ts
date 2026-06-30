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
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Subject, EMPTY } from 'rxjs';
import { catchError, debounceTime, switchMap, takeUntil, tap } from 'rxjs/operators';
import { AreasService, LocationSearchFilter } from '@core/services/areas/areas.service';
import { AreaMarkerDto } from '@shared/models/area/area-marker.model';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { CommonModule } from '@angular/common';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ImageTile } from 'ol';
import { ApiZoomLevel } from './map.types';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { AreaService } from '../../services/area/area.service';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MapToolbarComponent],
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

  public map!: NbicMapComponent;
  private areaDataCacheByApiZoom = new Map<number, AreaMarkerDto[]>();
  private mapReady = false;

  private destroy$ = new Subject<void>();
  private fetchAreaData$ = new Subject<{ apiZoomLevel: number; olZoom: number; extent: [number, number, number, number] }>();

  private readonly areasService = inject(AreasService);
  private readonly sharedMapService = inject(SharedMapService);
  private readonly logger = inject(LoggingService);
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);

  private readonly locationFilter = computed<LocationSearchFilter>(
    () => {
      const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();

      return {
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        countyIds: countyIds.length ? countyIds : undefined,
        municipalityIds: municipalityIds.length ? municipalityIds : undefined,
        oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
        coordinatePrecisionFrom: coordinatePrecisionFrom,
        coordinatePrecisionTo: coordinatePrecisionTo,
        periodFrom: periodFrom,
        periodTo: periodTo,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  private readonly _refetchOnFilterChange = effect(() => {
    this.locationFilter();
    if (this.mapReady) {
      this.areaDataCacheByApiZoom.clear();
      this.emitFetchEvent();
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

  private onMapReady(): void {
    this.mapReady = true;
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.setupAreaMarkerLayers();
    this.setupAreaDataPipeline();
    this.map.on(MapEvents.CameraChanged, (camera) => this.onCameraChanged(camera.zoom));
    this.emitFetchEvent();
  }

  private setupAreaMarkerLayers(): void {
    this.map.addLayer({
      id: this.COUNTIES_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 50,
      maxZoom: ZoomConfig.ZOOM_COUNTIES_THRESHOLD,
    });

    this.map.addLayer({
      id: this.MUNICIPALITIES_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 50,
      minZoom: ZoomConfig.ZOOM_COUNTIES_THRESHOLD,
      maxZoom: ZoomConfig.ZOOM_MUNICIPALITIES_THRESHOLD,
    });

    this.map.addLayer({
      id: this.LOCATIONS_LAYER_ID,
      kind: 'vector',
      source: { type: 'memory' },
      pickable: true,
      zIndex: 100,
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
  }

  private onCameraChanged(zoom: number): void {
    this.emitFetchEvent(zoom);
  }

  private emitFetchEvent(olZoom?: number): void {
    const currentZoom = olZoom ?? this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    const apiZoomLevel = ZoomConfig.getApiZoomLevel(currentZoom);
    const extent = this.map.getExtent() as [number, number, number, number];
    this.fetchAreaData$.next({ apiZoomLevel, olZoom: currentZoom, extent });
  }

  private setupAreaDataPipeline(): void {
    this.fetchAreaData$.pipe(
      debounceTime(300),
      switchMap(({ apiZoomLevel, olZoom, extent }) => {
        const isLocationPoints = apiZoomLevel === ApiZoomLevel.LocationPoints;

        // For områdemarkører: bruk cachet data og bygg GeoJSON med gjeldende kartutsnitt
        if (!isLocationPoints) {
          const cachedAreas = this.areaDataCacheByApiZoom.get(apiZoomLevel);
          if (cachedAreas) {
            const geojson = this.areasService.buildAreaGeoJson(cachedAreas, extent);
            this.applyGeoJsonToLayer(apiZoomLevel, geojson);
            return EMPTY;
          }

          const filter = this.locationFilter();
          return this.areasService.getAreaMarkers(olZoom, filter).pipe(
            tap(areas => {
              this.areaDataCacheByApiZoom.set(apiZoomLevel, areas);
              const geojson = this.areasService.buildAreaGeoJson(areas, extent);
              this.applyGeoJsonToLayer(apiZoomLevel, geojson);
            }),
            catchError((err: unknown) => {
              this.logger.error(`Failed to load area markers for API zoom level ${apiZoomLevel}:`, 'MapComponent', err);
              return EMPTY;
            })
          );
        }

        const filter = this.locationFilter();
        return this.areasService.getLocationsAsGeoJsonString(extent, filter).pipe(
          tap(geojson => {
            this.applyGeoJsonToLayer(apiZoomLevel, geojson);
          }),
          catchError((err: unknown) => {
            this.logger.error(`Failed to load location points:`, 'MapComponent', err);
            return EMPTY;
          })
        );
      }),
      takeUntil(this.destroy$),
    ).subscribe();
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

  private cleanup(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.areaDataCacheByApiZoom.clear();
    this.map?.destroy?.();
  }

  onIconClick(iconName: string): void {
    if (!iconName.startsWith(this.MAP_TYPE_PREFIX)) {
      return;
    }
    const layerId = iconName.slice(this.MAP_TYPE_PREFIX.length);
    if (!this.map || !layerId) return;
    this.map.setLayerVisibility(layerId, true);
  }

  ngOnDestroy(): void {
    this.cleanup();
  }
}
