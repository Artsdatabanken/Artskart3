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
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Subject, Observable } from 'rxjs';
import { switchMap, takeUntil, tap } from 'rxjs/operators';
import { AreasService } from '@core/services/areas/areas.service';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { MAP_CONFIG } from '@shared/config/map.config';
import { CommonModule } from '@angular/common';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ImageTile } from 'ol';
import { ApiZoomLevel } from './map.types';

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
  private previousApiZoomLevel: number | null = null;
  private geojsonCacheByApiZoom = new Map<number, string>();

  private destroy$ = new Subject<void>();
  private fetchZoomLevel$ = new Subject<number>();

  private readonly areasService = inject(AreasService);
  private readonly sharedMapService = inject(SharedMapService);
  private readonly logger = inject(LoggingService);

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
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.setupAreaMarkerLayers();
    this.setupAreaDataPipeline();
    this.map.on(MapEvents.CameraChanged, (camera) => this.onCameraChanged(camera.zoom));
    this.fetchZoomLevel$.next(this.getApiZoomLevel());
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
    });
  }

  private onCameraChanged(zoom: number): void {
    const apiZoomLevel = ZoomConfig.getApiZoomLevel(zoom);
    if (apiZoomLevel !== this.previousApiZoomLevel) {
      this.fetchZoomLevel$.next(apiZoomLevel);
    }
  }

  private getApiZoomLevel(): number {
    const currentZoom = this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    return ZoomConfig.getApiZoomLevel(currentZoom);
  }

  private setupAreaDataPipeline(): void {
    this.fetchZoomLevel$.pipe(
      tap(apiZoomLevel => this.previousApiZoomLevel = apiZoomLevel),
      switchMap(apiZoomLevel => {
        const cached = this.geojsonCacheByApiZoom.get(apiZoomLevel);
        if (cached) {
          this.applyGeoJsonToLayer(apiZoomLevel, cached);
          return [];
        }

        const currentZoom = this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
        const isLocationPoints = apiZoomLevel === ApiZoomLevel.LocationPoints;

        const serviceCall$: Observable<string> = isLocationPoints
          ? this.areasService.getLocationsAsGeoJsonString()
          : this.areasService.getAreasObservationsAsGeoJsonString(currentZoom);

        return serviceCall$.pipe(
          tap(geojson => {
            this.geojsonCacheByApiZoom.set(apiZoomLevel, geojson);
            this.applyGeoJsonToLayer(apiZoomLevel, geojson);
          })
        );
      }),
      takeUntil(this.destroy$),
    ).subscribe({
      error: (err: unknown) => {
        this.logger.error('Failed to load area markers:', 'MapComponent', err);
        this.mapReadyAction.emit(false);
      },
    });
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
    this.geojsonCacheByApiZoom.clear();
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
