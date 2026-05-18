import { createMap, MapEvents, nbicMapPresets } from '@artsdatabanken/nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AreasService } from '@core/services/areas/areas.service';
import type { AreaMarkerFeature } from '@shared/models/area/area-marker.model';
import { getAreaTypeColor } from '@shared/models/area/area-marker.model';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Feature from 'ol/Feature';
import { Point } from 'ol/geom';
import { Style, Circle, Fill, Stroke, Text } from 'ol/style';
import { ZoomCalculator, ZoomVisibilityHelper, ZoomState, MapZoomController } from '@shared/helpers/zoom';
import { AbbreviateNumberHelper } from '@shared/helpers/number/abbreviate-number.helper';
import { MAP_CONFIG } from '@shared/config/map.config';

type MapType = ReturnType<typeof createMap> | null;

@Component({
  selector: 'app-map',
  standalone: false,
  templateUrl: './map.component.html',
  styleUrl: './map.component.css',
})
export class MapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;
  @Output() mapReadyAction = new EventEmitter<boolean>();

  //private map: MapType = null;
  private zoomState: ZoomState = new ZoomState();
  private mapZoomController: MapZoomController | null = null;

  private destroy$ = new Subject<void>();
  private areaMarkersLayerId = MAP_CONFIG.areaMarkersLayer;
  private areaMarkerFeatures: AreaMarkerFeature[] = [];
  private areMarkersLoaded = false;
  private mouseMoveFn?: (e: MouseEvent) => void;
  private mouseLeaveFn?: () => void;

  tooltip = {
    visible: false,
    name: '',
    type: '',
    count: 0,
    x: 0,
    y: 0
  };

  private handleScrollWheel = (event: WheelEvent) => {
    event.preventDefault();
    this.handleScrollZoom(event);
  };

  private areasService = inject(AreasService);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any;

  ngAfterViewInit(): void {
    setTimeout(() => this.initializeMap(), MAP_CONFIG.initDelay);
  }

  /**
   * Configures base map layers (OSM and topographic)
   */
  private setupBaseMapLayers(): void {
    if (!this.map) return;
    this.map.addLayer(nbicMapPresets.osm);

    const topoLayer = { ...nbicMapPresets.topografiskBaseLayer, base: 'regional' as any };
    if (topoLayer.source.type === 'wmts') {
      topoLayer.source.options.projection = MAP_CONFIG.projection;
      topoLayer.source.options.matrixSet = MAP_CONFIG.projectionMatrix;
    }
    this.map.addLayer(topoLayer);
  }

  /**
   * Initializes map and sets up event listeners
   */
  private onMapReady(): void {
    this.mapReadyAction.emit(true);
    if (!this.map) return;
    this.map.activateHoverInfo();
    this.syncZoomFromMap();
    this.setupScrollZoom();
    this.setupAreaMarkers();
    this.setupZoomChangeListener();
  }

  private initializeMap(): void {
    try {
      if (!this.mapEl || !this.mapEl.nativeElement) {
        return;
      }

      this.map = createMap(
        this.mapEl.nativeElement,
        {
          version: 1,
          id: MAP_CONFIG.mapId,
          projection: MAP_CONFIG.projection,
          center: MAP_CONFIG.center,
          zoom: this.zoomState.getZoom(),
          minZoom: MAP_CONFIG.minZoom,
          maxZoom: MAP_CONFIG.maxZoom,
          controls: { scaleLine: true, fullscreen: true, geolocation: true, zoom: true, attribution: true },
        }
      );

      this.mapZoomController = new MapZoomController(this.map);
      this.setupBaseMapLayers();
      this.map.on(MapEvents.Ready, () => this.onMapReady());
    } catch (error) {
      console.error('Failed to initialize map:', error);
    }
  }

  private setupScrollZoom(): void {
    if (!this.mapEl) return;
    this.mapEl.nativeElement.addEventListener('wheel', this.handleScrollWheel, { passive: false });
  }

  private syncZoomFromMap(): void {
    if (!this.map || !this.mapZoomController) return;
    const zoom = this.mapZoomController.getCurrentZoom();
    if (zoom !== null && !isNaN(zoom)) {
      this.zoomState.setZoom(zoom);
    }
  }

  private handleScrollZoom(event: WheelEvent): void {
    if (!this.map || !this.mapZoomController) return;
    if (!this.zoomState.canProcessScroll()) return;
    const newTargetZoom = this.zoomState.calculateScrollZoom(
      event.deltaY,
      this.mapZoomController.getMinZoom(),
      this.mapZoomController.getMaxZoom()
    );
    if (newTargetZoom === null) return;
    this.animateZoom(newTargetZoom);
  }

  /**
   * Animates zoom with requestAnimationFrame for smooth transitions
   */
  private animateZoom(targetZoom: number): void {
    this.zoomState.cancelAnimation();

    const startZoom = this.zoomState.getZoom();
    const startTime = performance.now();

    // If zoom difference is negligible, apply immediately
    if (ZoomCalculator.isZoomDifferenceNegligible(targetZoom, startZoom)) {
      this.applyZoom(targetZoom);
      this.zoomState.setZoom(targetZoom);
      return;
    }

    const animate = (currentTime: number) => {
      const elapsed = currentTime - startTime;
      const progress = ZoomCalculator.calculateAnimationProgress(elapsed);
      const currentZoom = ZoomCalculator.interpolateZoom(startZoom, targetZoom, progress);

      this.applyZoom(currentZoom);
      this.zoomState.setZoom(currentZoom);

      if (progress < 1) {
        const frameId = requestAnimationFrame(animate);
        this.zoomState.setAnimationFrameId(frameId);
      } else {
        this.zoomState.setZoom(targetZoom);
        this.zoomState.setAnimationFrameId(null);
      }
    };

    const frameId = requestAnimationFrame(animate);
    this.zoomState.setAnimationFrameId(frameId);
  }

  private applyZoom(zoom: number): void {
    if (!this.map || !this.mapZoomController || isNaN(zoom)) return;
    try {
      this.mapZoomController.setZoom(zoom);
      this.zoomState.setZoom(zoom);
      this.updateMarkerLayersIfNeeded(zoom);
    } catch (error) {
      console.error('Error applying zoom:', error);
    }
  }

  /**
   * Recreates marker layers if zoom crosses visibility thresholds
   */
  private updateMarkerLayersIfNeeded(zoom: number): void {
    if (!this.areMarkersLoaded || this.areaMarkerFeatures.length === 0) {
      return;
    }

    const shouldShowCounties = ZoomVisibilityHelper.shouldShowCounties(zoom);
    const shouldShowMunicipalities = ZoomVisibilityHelper.shouldShowMunicipalities(zoom);
    const countiesLayerId = `${this.areaMarkersLayerId}${MAP_CONFIG.countiesSuffix}`;
    const municipalitiesLayerId = `${this.areaMarkersLayerId}${MAP_CONFIG.municipalitiesSuffix}`;

    const countiesLayerExists = !!this.mapZoomController?.getLayerById(countiesLayerId);
    const municipalitiesLayerExists = !!this.mapZoomController?.getLayerById(municipalitiesLayerId);

    // Check if layer visibility matches current zoom
    const needsRecreate =
      (shouldShowCounties && !countiesLayerExists && municipalitiesLayerExists) ||
      (shouldShowMunicipalities && !municipalitiesLayerExists && countiesLayerExists) ||
      (shouldShowCounties && !countiesLayerExists) ||
      (shouldShowMunicipalities && !municipalitiesLayerExists) ||
      (shouldShowCounties && municipalitiesLayerExists) ||
      (shouldShowMunicipalities && countiesLayerExists);

    if (needsRecreate) {
      this.areMarkersLoaded = false;
      this.addMarkerLayer();
      this.zoomState.updateLastZoom();
    }
  }

  public getZoomLevel(): number {
    return this.zoomState.getZoom();
  }

  /**
   * Handles successful loading of area marker features
   */
  private onAreaMarkersLoaded(features: AreaMarkerFeature[]): void {
    if (features.length === 0) {
      return;
    }

    this.areaMarkerFeatures = features;

    if (this.map) {
      this.addMarkerLayer();
    }
  }

  /**
   * Loads area markers from service and triggers layer creation
   */
  private setupAreaMarkers(): void {
    if (this.areMarkersLoaded) {
      return;
    }

    this.areasService.getAreasAsGeoJson()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (features: AreaMarkerFeature[]) => this.onAreaMarkersLoaded(features),
        error: (err) => {
          console.error('Failed to load area markers:', err);
          this.mapReadyAction.emit(false);
        }
      });
  }

  /**
   * Creates and adds marker layers based on current zoom level
   */
  private addMarkerLayer(): void {
    if (!this.map || !this.areaMarkerFeatures.length) {
      return;
    }

    try {
      const currentZoom = this.zoomState.getZoom();
      const countiesLayerId = `${this.areaMarkersLayerId}${MAP_CONFIG.countiesSuffix}`;
      const municipalitiesLayerId = `${this.areaMarkersLayerId}${MAP_CONFIG.municipalitiesSuffix}`;

      // Remove existing layers
      this.mapZoomController?.removeLayer(countiesLayerId);
      this.mapZoomController?.removeLayer(municipalitiesLayerId);

      // Add layers based on current zoom level
      if (ZoomVisibilityHelper.shouldShowCounties(currentZoom)) {
        const countiesFeatures = this.areaMarkerFeatures.filter(f => f.properties.areaTypeId === 2);
        this.createMarkerLayer(countiesFeatures, countiesLayerId, 2);
      }

      if (ZoomVisibilityHelper.shouldShowMunicipalities(currentZoom)) {
        const municipalitiesFeatures = this.areaMarkerFeatures.filter(f => f.properties.areaTypeId === 1);
        this.createMarkerLayer(municipalitiesFeatures, municipalitiesLayerId, 1);
      }

      this.areMarkersLoaded = true;
    } catch (error) {
      console.error('Error adding marker layer:', error);
    }
  }

  /**
   * Creates styled marker circle with observation count label
   */
  private createMarkerStyle(areaTypeId: number, formattedCount: string): Style {
    return new Style({
      image: new Circle({
        radius: 18,
        fill: new Fill({ color: getAreaTypeColor(areaTypeId) }),
        stroke: new Stroke({ color: '#FFFFFF', width: 2 })
      }),
      text: new Text({
        text: formattedCount,
        fill: new Fill({ color: '#FFFFFF' }),
        font: 'bold 10px Arial',
        textAlign: 'center',
        offsetX: 0,
        offsetY: 0
      })
    });
  }

  /**
   * Creates and renders marker layer with styled features
   */
  private createMarkerLayer(features: AreaMarkerFeature[], layerId: string, areaTypeId: number): void {
    if (features.length === 0) return;

    const source = new VectorSource();

    for (const feature of features) {
      const coord = feature.geometry.coordinates;
      const count = feature.properties.observationCount || 0;
      const formattedCount = AbbreviateNumberHelper.format(count);

      const olFeature = new Feature({
        geometry: new Point(coord),
        ...feature.properties,
        fromLayer: layerId
      });

      olFeature.setStyle(this.createMarkerStyle(areaTypeId, formattedCount));
      source.addFeature(olFeature);
    }

    const layer = new VectorLayer({
      source,
      properties: { id: layerId }
    });

    if (this.map) {
      this.map.adoptLayer(layerId, layer);
    }
    this.setupMarkerInteractions(layerId);
  }

  private showTooltip(name: string, areaTypeName: string, observationCount: number, x: number, y: number): void {
    this.tooltip.name = name || 'Unknown';
    this.tooltip.type = areaTypeName || 'Area';
    this.tooltip.count = observationCount || 0;
    this.tooltip.x = x + MAP_CONFIG.tooltipOffset;
    this.tooltip.y = y + MAP_CONFIG.tooltipOffset;
    this.tooltip.visible = true;
  }

  private hideTooltip(): void {
    this.tooltip.visible = false;
  }

  /**
   * Checks if tooltip should be shown for hovered layer
   */
  private processHoverForLayer(layerId: string, items: any[], mouseX: number, mouseY: number): boolean {
    for (const item of items) {
      const feature = item.feature;
      if (!feature) continue;

      const props = feature.getProperties?.() || feature.properties || {};
      const featureLayerId = props.fromLayer || feature.layer?.get('id');

      if (featureLayerId === layerId && props.name) {
        this.showTooltip(props.name, props.areaTypeName, props.observationCount, mouseX, mouseY);
        return true;
      }
    }
    return false;
  }

  /**
   * Sets up hover interactions for marker layer
   */
  private setupMarkerInteractions(layerId: string): void {
    if (!this.map || !this.mapEl?.nativeElement) return;

    try {
      const mapElement = this.mapEl.nativeElement;

      this.mouseMoveFn = (e: MouseEvent) => {
        const mouseX = e.clientX;
        const mouseY = e.clientY;
      };

      this.mouseLeaveFn = () => {
        this.hideTooltip();
      };

      mapElement.addEventListener('mousemove', this.mouseMoveFn);
      mapElement.addEventListener('mouseleave', this.mouseLeaveFn);

      this.map.on('hover:info', (event: any) => {
        if (!event?.items || event.items.length === 0) {
          this.hideTooltip();
          return;
        }

        if (!this.processHoverForLayer(layerId, event.items, 0, 0)) {
          this.hideTooltip();
        }
      });
    } catch (error) {
      console.error('Error setting up marker interactions:', error);
    }
  }

  /**
   * Sets up listener for map zoom changes to update marker visibility
   */
  private setupZoomChangeListener(): void {
    if (!this.map) return;

    this.zoomState.updateLastZoom();

    this.map.on('moveend' as any, () => {
      this.syncZoomFromMap();

      if (ZoomVisibilityHelper.hasCrossedThreshold(this.zoomState.getLastZoom(), this.zoomState.getZoom())) {
        this.areMarkersLoaded = false;
        this.addMarkerLayer();
      }

      this.zoomState.updateLastZoom();
    });
  }

  /**
   * Cleans up animation frame
   */
  private cleanupAnimation(): void {
    this.zoomState.cancelAnimation();
  }

  /**
   * Cleans up event listeners
   */
  private cleanupEventListeners(): void {
    if (this.mapEl?.nativeElement) {
      this.mapEl.nativeElement.removeEventListener('wheel', this.handleScrollWheel);
      if (this.mouseMoveFn) {
        this.mapEl.nativeElement.removeEventListener('mousemove', this.mouseMoveFn);
      }
      if (this.mouseLeaveFn) {
        this.mapEl.nativeElement.removeEventListener('mouseleave', this.mouseLeaveFn);
      }
    }
  }

  /**
   * Cleans up map resources
   */
  private cleanupMap(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.map?.destroy?.();
  }

  ngOnDestroy(): void {
    this.cleanupAnimation();
    this.cleanupEventListeners();
    this.cleanupMap();
  }
}
