import { createMap, MapEvents, nbicMapPresets } from '@artsdatabanken/nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy } from '@angular/core';

@Component({
  selector: 'app-map',
  standalone: false,
  templateUrl: './map.component.html',
  styleUrl: './map.component.css',
})
export class MapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('mapEl', { static: false }) mapEl!: ElementRef<HTMLDivElement>;
  @Output() mapReadyAction = new EventEmitter<boolean>();
  private map: any;

  ngAfterViewInit(): void {
    setTimeout(() => this.initializeMap(), 100);
  }

  private initializeMap(): void {
    try {
      if (!this.mapEl || !this.mapEl.nativeElement) {
        return;
      }

      // Initialize map with UTM Zone 33N for Norway
      this.map = createMap(
        this.mapEl.nativeElement,
        {
          version: 1,
          id: 'artskart-map',
          projection: 'EPSG:25833',
          center: [300000, 7220000],  // Centered on Norway in UTM 33N
          zoom: 6.2,
          minZoom: 0,
          maxZoom: 18,
          controls: { scaleLine: true, fullscreen: true, geolocation: true, zoom: true, attribution: true },
        }
      );

      // Add base layers
      this.map.addLayer(nbicMapPresets.osm);

      const topoLayer = { ...nbicMapPresets.topografiskBaseLayer, base: 'regional' };
      if (topoLayer.source.type === 'wmts') {
        topoLayer.source.options.projection = 'EPSG:25833';
        topoLayer.source.options.matrixSet = 'utm33n';
      }
      this.map.addLayer(topoLayer);

      this.map.on(MapEvents.Ready, () => {
        this.mapReadyAction.emit(true);
        this.map.activateHoverInfo();
      });
    } catch (error) {
      console.error('Error initializing map:', error);
    }
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.destroy?.();
    }
  }
}
