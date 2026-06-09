import { createMap, MapEvents, nbicMapPresets, NbicMapComponent, LayerDef } from '@artsdatabanken/nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAP_LAYER_CONFIGS, MapLayerConfig } from '../../config/map/map-layer.config';
import { SharedMapService } from '../../services/shared-map.service';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ImageTile } from 'ol';

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
  public map!: NbicMapComponent;

  private readonly PROJECTION = 'EPSG:25833';
  private readonly MATRIX_SET = 'utm33n';
  private readonly DEFAULT_CENTER: [number, number] = [300000, 7220000];
  private readonly DEFAULT_ZOOM = 6.2;
  private readonly MAP_TYPE_PREFIX = 'map-type:';

  private sharedMapService = inject(SharedMapService);

  ngAfterViewInit(): void {
    setTimeout(() => this.initializeMap(), 100);
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
          id: 'artskart-map',
          projection: this.PROJECTION,
          center: this.DEFAULT_CENTER,
          zoom: this.DEFAULT_ZOOM,
          minZoom: 0,
          maxZoom: 18,
          controls: { scaleLine: true, fullscreen: false, geolocation: true, zoom: false, attribution: true },
        }
      );

      this.generateBackgroundMaps();
      this.map.on(MapEvents.Ready, () => {
        this.mapReadyAction.emit(true);
        this.map!.activateHoverInfo();
      });
    } catch (error) {
      console.error('Error initializing map:', error);
    }
  }

  private generateBackgroundMaps(): void {
    const osmLayer = { ...nbicMapPresets.osm, id: 'osm' };
    this.map.addLayer(osmLayer);

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
    if (layer.source.type !== 'wmts') return { ...layer, base: 'regional' as const, id }

    const originalOptions = layer.source.options;
    const configuredLayer: LayerDef = {
     ...layer,
     base: 'regional' as const,
     id,
     source: {
       ...layer.source,
       options: {
         ...originalOptions,
       },
     },
   };
   return configuredLayer;
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
      console.warn(`Unknown map layer: ${layerId}`);
      return;
    }

    this.updateMapLayers(config);
  }

  private updateMapLayers(config: MapLayerConfig): void {
    const baseLayerIds = this.map.getBaseLayerIds();
    baseLayerIds.forEach((id: string) => {
      this.map.setLayerVisibility(id, config.visibleLayers.includes(id));
    });

    this.map.setCenter(config.center);
    this.map.setZoom(config.zoom);
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.destroy?.();
    }
  }
}
