import { createMap, LayerDef, MapEvents, NbicMapComponent, nbicMapPresets } from '@artsdatabanken/nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy, inject } from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Subject, Observable } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AreasService } from '@core/services/areas/areas.service';
import type { AreaMarkerFeature } from '@shared/models/area/area-marker.model';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Feature from 'ol/Feature';
import { Polygon, Point } from 'ol/geom';
import { Style, Fill, Stroke, Text, Icon } from 'ol/style';
import { transform } from 'ol/proj';
import { ZoomVisibilityHelper, ZoomConfig } from '@shared/helpers/zoom';
import { AbbreviateNumberHelper } from '@shared/helpers/number/abbreviate-number.helper';
import { MAP_CONFIG } from '@shared/config/map.config';
import { CommonModule } from '@angular/common';
import { MAP_LAYER_CONFIGS, MapLayerConfig } from '../../config/map/map-layer.config';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ImageTile } from 'ol';
import { LayerConfig, AreaTypeId, ApiZoomLevel, AreaTypeStyle } from './map.types';

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
  private readonly AREA_TYPE_STYLES: Map<AreaTypeId, AreaTypeStyle> = new Map<AreaTypeId, AreaTypeStyle>([
    [AreaTypeId.LocationPoints, { zIndex: 100, fontSize: '9px' }],
    [AreaTypeId.County, { zIndex: 50, fontSize: '11px' }],
    [AreaTypeId.Municipality, { zIndex: 50, fontSize: '9px' }]
  ]);

  private readonly isLocationPointsZoom = (apiZoomLevel: number): boolean => apiZoomLevel === ApiZoomLevel.LocationPoints;

  public map!: NbicMapComponent;
  private previousApiZoomLevel: number | null = null;

  private areaMarkerFeatures: AreaMarkerFeature[] = [];
  private featuresCacheByApiZoom = new Map<number, AreaMarkerFeature[]>();

  private destroy$ = new Subject<void>();

  private areasService: AreasService = inject(AreasService);
  private sharedMapService: SharedMapService = inject(SharedMapService);
  private logger: LoggingService = inject(LoggingService);

  private getLayerConfig(): LayerConfig {
    const baseLayerId = MAP_CONFIG.areaMarkersLayer;
    return {
      countiesLayerId: `${baseLayerId}${MAP_CONFIG.countiesSuffix}`,
      municipalitiesLayerId: `${baseLayerId}${MAP_CONFIG.municipalitiesSuffix}`,
      locationPointsLayerId: `${baseLayerId}-location-points`
    };
  }

  private handleScrollWheel = (event: WheelEvent) => {
    event.preventDefault();
    this.handleScrollZoom(event);
  };

  ngAfterViewInit(): void {
    setTimeout(() => this.initializeMap(), MAP_CONFIG.initDelay);
  }

  private setupBaseMapLayers(): void {
    if (!this.map) return;
    this.map.addLayer(nbicMapPresets.osm);

    const topoLayer = { ...nbicMapPresets.topografiskBaseLayer, base: 'regional' as const };
    if (topoLayer.source.type === 'wmts') {
      topoLayer.source.options.projection = MAP_CONFIG.projection;
      topoLayer.source.options.matrixSet = MAP_CONFIG.projectionMatrix;
    }
    this.map.addLayer(topoLayer);
  }

  private onMapReady(): void {
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.syncZoomFromMap();
    this.setupScrollZoom();
    this.setupAreaMarkers();
  }

  private initializeMap(): void {
    try {
      if (!this.mapEl?.nativeElement) {
        return;
      }

      this.map = createMap(
        this.mapEl.nativeElement,
        {
          version: 1,
          id: MAP_CONFIG.mapId,
          projection: MAP_CONFIG.projection,
          center: MAP_CONFIG.center,
          zoom: ZoomConfig.DEFAULT_ZOOM_LEVEL,
          minZoom: MAP_CONFIG.minZoom,
          maxZoom: MAP_CONFIG.maxZoom,
          controls: { scaleLine: true, fullscreen: false, geolocation: true, zoom: false, attribution: true }
        }
      );

      this.setupBaseMapLayers();
      this.generateBackgroundMaps();
      this.map.on(MapEvents.Ready, () => this.onMapReady());
    } catch (error: unknown) {
      this.logger.error('Failed to initialize map:', 'MapComponent', error);
    }
  }

  private setupScrollZoom(): void {
    if (!this.mapEl) return;
    this.mapEl.nativeElement.addEventListener('wheel', this.handleScrollWheel, { passive: false });
  }

  private syncZoomFromMap(): void {
    if (!this.map) return;
  }

  private handleScrollZoom(event: WheelEvent): void {
    if (!this.map) return;
    const currentZoom = this.map.getCamera().zoom;
    const delta = ZoomConfig.calculateZoomDelta(event.deltaY);
    const newTargetZoom = ZoomConfig.constrainZoom(
      currentZoom + delta,
      MAP_CONFIG.minZoom,
      MAP_CONFIG.maxZoom
    );
    if (newTargetZoom === currentZoom) return;
    this.applyZoom(newTargetZoom);
  }

  onToolbarZoomChange(newZoom: number): void {
    const constrainedZoom = ZoomConfig.constrainZoom(
      newZoom,
      MAP_CONFIG.minZoom,
      MAP_CONFIG.maxZoom
    );
    this.applyZoom(constrainedZoom);
  }


  private applyZoom(zoom: number): void {
    if (!this.map || isNaN(zoom)) return;
    try {
      const previousZoom = this.map.getCamera().zoom;
      this.map.setZoom(zoom);

      if (ZoomVisibilityHelper.hasCrossedThreshold(previousZoom, zoom)) {
        this.setupAreaMarkers();
      } else if (this.areaMarkerFeatures.length > 0) {
        this.updateMarkerLayerVisibility(zoom);
      }
    } catch (error: unknown) {
      this.logger.error('Error applying zoom:', 'MapComponent', error);
    }
  }

  private updateMarkerLayerVisibility(zoom: number): void {
    const layerConfig = this.getLayerConfig();
    const apiZoomLevel = this.getApiZoomLevel();
    const apiZoomLevelChanged = this.previousApiZoomLevel !== null && this.previousApiZoomLevel !== apiZoomLevel;

    if (apiZoomLevelChanged) {
      this.previousApiZoomLevel = apiZoomLevel;
      this.setupAreaMarkers();
    } else if (this.shouldRedrawLayers(zoom, layerConfig, apiZoomLevel)) {
      this.previousApiZoomLevel = apiZoomLevel;
      this.addMarkerLayer();
    } else {
      this.previousApiZoomLevel = apiZoomLevel;
    }
  }

  private shouldRedrawLayers(zoom: number, layerConfig: LayerConfig, apiZoomLevel: number): boolean {
    const shouldShowCounties = ZoomVisibilityHelper.shouldShowCounties(zoom);
    const shouldShowMunicipalities = ZoomVisibilityHelper.shouldShowMunicipalities(zoom);
    const shouldShowLocationPoints = this.isLocationPointsZoom(apiZoomLevel);

    const countiesExists = !!this.map?.getLayerById(layerConfig.countiesLayerId);
    const municipalitiesExists = !!this.map?.getLayerById(layerConfig.municipalitiesLayerId);
    const locationPointsExists = !!this.map?.getLayerById(layerConfig.locationPointsLayerId);

    return (shouldShowCounties !== countiesExists) ||
           (shouldShowMunicipalities !== municipalitiesExists) ||
           (shouldShowLocationPoints !== locationPointsExists);
  }



  private getApiZoomLevel(): number {
    const currentZoom = this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
    return ZoomConfig.getApiZoomLevel(currentZoom);
  }

  private setupAreaMarkers(): void {
    const apiZoomLevel = this.getApiZoomLevel();
    const currentZoom = this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;

    if (this.previousApiZoomLevel === null) {
      this.previousApiZoomLevel = apiZoomLevel;
    }

    const cachedFeatures = this.featuresCacheByApiZoom.get(apiZoomLevel);
    if (cachedFeatures) {
      this.areaMarkerFeatures = cachedFeatures;
      this.addMarkerLayer();
      return;
    }

    this.fetchAndCacheMarkerFeatures(apiZoomLevel, currentZoom);
  }

  private fetchAndCacheMarkerFeatures(apiZoomLevel: number, currentZoom: number): void {
    const serviceCall$: Observable<AreaMarkerFeature[]> = this.isLocationPointsZoom(apiZoomLevel)
      ? this.areasService.getLocationsAsGeoJson()
      : this.areasService.getAreasObservationsAsGeoJson(currentZoom);

    serviceCall$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (features: AreaMarkerFeature[]) => this.onFeaturesReceived(features, apiZoomLevel),
        error: (err: unknown) => this.onFeaturesError(err)
      });
  }

  private onFeaturesReceived(features: AreaMarkerFeature[], apiZoomLevel: number): void {
    let processedFeatures = features;
    if (this.isLocationPointsZoom(apiZoomLevel)) {
      processedFeatures = features.filter(f => f.geometry.type === 'Point');
    }

    this.featuresCacheByApiZoom.set(apiZoomLevel, processedFeatures);
    this.areaMarkerFeatures = processedFeatures;

    if (this.map) {
      this.addMarkerLayer();
    }
  }

  private onFeaturesError(err: unknown): void {
    this.logger.error('Failed to load area markers:', 'MapComponent', err as unknown);
    this.mapReadyAction.emit(false);
  }


  private removeAllMarkerLayers(): void {
    const layerConfig = this.getLayerConfig();
    [
      layerConfig.countiesLayerId,
      layerConfig.municipalitiesLayerId,
      layerConfig.locationPointsLayerId
    ].forEach(layerId => this.map?.removeLayer(layerId));
  }

  private addMarkerLayer(): void {
    if (!this.map || !this.areaMarkerFeatures.length) return;

    try {
      const currentZoom = this.map?.getCamera().zoom ?? ZoomConfig.DEFAULT_ZOOM_LEVEL;
      const apiZoomLevel = this.getApiZoomLevel();
      const layerConfig = this.getLayerConfig();

      this.removeAllMarkerLayers();

      if (this.isLocationPointsZoom(apiZoomLevel)) {
        if (this.areaMarkerFeatures.length > 0) {
          this.createMarkerLayer(this.areaMarkerFeatures, layerConfig.locationPointsLayerId, AreaTypeId.LocationPoints);
        }
        return;
      }

      this.addLayerIfFeaturesExist(ZoomVisibilityHelper.shouldShowCounties(currentZoom), AreaTypeId.County, layerConfig.countiesLayerId);
      this.addLayerIfFeaturesExist(ZoomVisibilityHelper.shouldShowMunicipalities(currentZoom), AreaTypeId.Municipality, layerConfig.municipalitiesLayerId);
    } catch (error: unknown) {
      this.logger.error('Error adding marker layer:', 'MapComponent', error);
    }
  }

  private addLayerIfFeaturesExist(shouldShow: boolean, areaTypeId: AreaTypeId, layerId: string): void {
    if (shouldShow) {
      const features = this.filterFeaturesByType(areaTypeId);
      if (features.length > 0) {
        this.createMarkerLayer(features, layerId, areaTypeId);
      }
    }
  }

  private filterFeaturesByType(areaTypeId: number): AreaMarkerFeature[] {
    return this.areaMarkerFeatures.filter(f => f.properties.areaTypeId === areaTypeId);
  }

  private createPolygonStyle(): Style {
    return new Style({
      stroke: new Stroke({ color: 'rgba(10, 109, 188, 0.6)', width: 1.5 })
    });
  }

  private createMarkerStyle(areaTypeId: AreaTypeId, formattedCount: string): Style {
    const styleConfig = this.AREA_TYPE_STYLES.get(areaTypeId) || { fontSize: '7px' };
    const markerSvgUri = 'data:image/svg+xml;utf8,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 50 50" fill="none"><circle cx="25" cy="25" r="24.25" fill="#005A71" stroke="#D2DDE0" stroke-width="1.5"/></svg>');

    return new Style({
      image: new Icon({
        src: markerSvgUri,
        scale: 1,
        anchor: [0.5, 0.5]
      }),
      text: new Text({
        text: formattedCount,
        fill: new Fill({ color: '#FFFFFF' }),
        font: `bold ${styleConfig.fontSize} Arial`,
        textAlign: 'center',
        textBaseline: 'middle'
      })
    });
  }


  private calculatePolygonCentroid(coordinates: number[][][]): [number, number] {
    if (!coordinates || coordinates.length === 0) return [0, 0];

    const ring = coordinates[0];
    let x = 0, y = 0;

    for (const coord of ring) {
      x += coord[0];
      y += coord[1];
    }

    return [x / ring.length, y / ring.length];
  }

  private createMarkerLayer(features: AreaMarkerFeature[], layerId: string, areaTypeId: AreaTypeId): void {
    if (features.length === 0) return;

    try {
      if (areaTypeId === AreaTypeId.LocationPoints) {
        this.createLocationPointsLayer(features, layerId);
      } else {
        this.createPolygonAreaLayer(features, layerId, areaTypeId);
      }
    } catch (error: unknown) {
      this.logger.error('Error creating marker layer:', 'MapComponent', error);
    }
  }

  private createLocationPointsLayer(features: AreaMarkerFeature[], layerId: string): void {
    const pointFeatures = features.filter(f => f.geometry.type === 'Point');
    if (pointFeatures.length === 0) {
      return;
    }

    const circlePointSvgUri = 'data:image/svg+xml;utf8,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="19" height="19" viewBox="0 0 19 19" fill="none"><circle cx="9.5" cy="9.5" r="8.5" fill="#005A71" stroke="#D2DDE0" stroke-width="2"/></svg>');
    const redCircleStyle = new Style({
      image: new Icon({
        src: circlePointSvgUri,
        scale: 1,
        anchor: [0.5, 0.5]
      })
    });

    const source = this.createLocationPointVectorSource(pointFeatures, redCircleStyle);
    const zIndex = this.AREA_TYPE_STYLES.get(AreaTypeId.LocationPoints)?.zIndex || 100;
    const layer = new VectorLayer({
      source,
      properties: { id: layerId },
      zIndex
    });

    this.map?.adoptLayer(layerId, layer, {
      role: 'overlay',
      pickable: true,
      zIndex
    });
  }

  private createLocationPointVectorSource(features: AreaMarkerFeature[], style: Style): VectorSource {
    const source = new VectorSource();

    features.forEach(feature => {
      const coordinates = feature.geometry.coordinates as number[];
      const transformedCoords = transform([coordinates[0], coordinates[1]], 'EPSG:4326', 'EPSG:25833');

      const markerFeature = new Feature({
        geometry: new Point(transformedCoords),
        name: feature.properties.name,
        count: feature.properties.observationCount
      });
      markerFeature.setStyle(style);
      source.addFeature(markerFeature);
    });

    return source;
  }

  private createPolygonAreaLayer(features: AreaMarkerFeature[], layerId: string, areaTypeId: AreaTypeId): void {
    const source = this.createPolygonAreaVectorSource(features, areaTypeId);
    if (source === null) return;

    const zIndex = this.AREA_TYPE_STYLES.get(areaTypeId)?.zIndex || 50;
    const layer = new VectorLayer({
      source,
      properties: { id: layerId },
      zIndex
    });

    this.map?.adoptLayer(layerId, layer, {
      role: 'overlay',
      pickable: true,
      zIndex
    });
  }

  /**
   * Creates VectorSource for polygon areas with centroids ??? fjerne etter på
   */
  private createPolygonAreaVectorSource(features: AreaMarkerFeature[], areaTypeId: AreaTypeId): VectorSource | null {
    const source = new VectorSource();
    let validFeatureCount = 0;

    features.forEach(feature => {
      if (feature.geometry.type === 'Point') return;

      const count = feature.properties.observationCount || 0;
      const formattedCount = AbbreviateNumberHelper.format(count);
      const polygonCoordinates = feature.geometry.coordinates as number[][][];
      const centroid = this.calculatePolygonCentroid(polygonCoordinates);

      // Add polygon boundary
      const polygonFeature = new Feature({
        geometry: new Polygon(polygonCoordinates),
        ...feature.properties,
        fromLayer: `${MAP_CONFIG.areaMarkersLayer}${MAP_CONFIG.countiesSuffix}`
      });
      polygonFeature.setStyle(this.createPolygonStyle());
      source.addFeature(polygonFeature);

      // Add marker at centroid
      const markerFeature = new Feature({
        geometry: new Point(centroid),
        ...feature.properties,
        fromLayer: `${MAP_CONFIG.areaMarkersLayer}${MAP_CONFIG.countiesSuffix}`
      });
      markerFeature.setStyle(this.createMarkerStyle(areaTypeId, formattedCount));
      source.addFeature(markerFeature);
      validFeatureCount++;
    });

    return validFeatureCount > 0 ? source : null;
  }

  private cleanup(): void {
    this.cleanupEventListeners();
    this.destroy$.next();
    this.destroy$.complete();
    this.featuresCacheByApiZoom.clear();
    this.map?.destroy?.();
  }

  private cleanupEventListeners(): void {
    if (!this.mapEl?.nativeElement) return;
    this.mapEl.nativeElement.removeEventListener('wheel', this.handleScrollWheel);
  }

  /**
   * Generates and configures background map layers with proper token handling for NIB layer
   */
  private generateBackgroundMaps(): void {
    if (!this.map) return;

    const nib = { ...nbicMapPresets.nib, id: 'nib' };
    const nibSource = nib.source;

    if (nibSource.type === 'wmts') {
      nibSource.options.tileLoadFunction = (tile, src) => {
        const token = this.sharedMapService.getNibToken();
        const separator = src.includes('?') ? '&' : '?';
        const img = (tile as ImageTile).getImage() as HTMLImageElement;
        img.src = token ? `${src}${separator}token=${token}` : src;
      };
    }

    this.map.addLayer(this.createWmtsLayer(nbicMapPresets.topografiskBaseLayer, 'topografiskBaseLayer'));
    this.map.addLayer(this.createWmtsLayer(nbicMapPresets.topo4graatoneBaseLayer, 'topo4graatoneBaseLayer'));
    this.map.addLayer(this.createWmtsLayer(nbicMapPresets.svalbardBaseLayer, 'svalbardBaseLayer'));
    this.map.addLayer(this.createWmtsLayer(nbicMapPresets.janmayenBaseLayer, 'janmayenBaseLayer'));
    this.map.addLayer(nib);
  }

  private createWmtsLayer(layer: LayerDef, id: string): LayerDef {
    if (layer.source.type !== 'wmts') {
      return { ...layer, base: 'regional' as const, id };
    }

    return {
      ...layer,
      base: 'regional' as const,
      id,
      source: {
        ...layer.source,
        options: {
          ...layer.source.options,
          projection: MAP_CONFIG.projection,
          matrixSet: MAP_CONFIG.projectionMatrix
        }
      }
    };
  }

  onIconClick(iconName: string): void {
    if (!iconName.startsWith(this.MAP_TYPE_PREFIX)) {
      return;
    }
    const layerId = iconName.slice(this.MAP_TYPE_PREFIX.length);
    this.handleMapTypeChange(layerId);
  }

  private handleMapTypeChange(layerId: string): void {
    if (!this.map || !layerId) return;

    const config = MAP_LAYER_CONFIGS[layerId];
    if (!config) {
      this.logger.warn(`Unknown map layer: ${layerId}`, 'MapComponent');
      return;
    }

    this.updateMapLayers(config);
  }

  private updateMapLayers(config: MapLayerConfig): void {
    const baseLayerIds = this.map.getBaseLayerIds();
    baseLayerIds.forEach((id: string) => {
      this.map.setLayerVisibility(id, config.visibleLayers.includes(id));
    });
  }

  ngOnDestroy(): void {
    this.cleanup();
  }
}
