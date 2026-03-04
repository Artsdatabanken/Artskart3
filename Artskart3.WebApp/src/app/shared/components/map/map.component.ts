import { createMap, MapEvents, nbicMapPresets } from 'nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy } from '@angular/core';
import { MapConfig } from '../../models/mapconfig.model';

@Component({
  selector: 'app-map',
  standalone: false,
  templateUrl: './map.component.html',
  styleUrl: './map.component.css',
})
export class MapComponent implements AfterViewInit, OnDestroy {
  readonly DATA_PROJ = 'EPSG:25833' as const;
  readonly matrixSet = 'utm33n' as const;
  readonly initZoom = 6.2 as const;
  readonly initCenter: [number, number] =  [300000, 7220000] as const; // Centered on Norway in UTM 33N

  // Background layers
  mapOverlays: any[] = []//LayerDef[] = [];
  mapConfig: MapConfig= {
    id: 'view-sighting',
    center: nbicMapPresets.norwayCoordinate,
    zoom: 4,
    maxZoom: 18,
    polygonType: 'box',
    tabIndex: 0,
    controls: {
      fullScreen: true,
      layerSelector: true,
      overlaysInlayerSelector: false,
      locationSearchResult: true,
      drawPolygon: true,
      mapClick: true,
      geoLocation: true,
      zoomToGeoLocation: true,
      legend: true
    },
    wfsLayers: [
      nbicMapPresets.administrativeEnheterKommune,
      nbicMapPresets.administrativeEnheterFylke
    ]
  };

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
          projection: this.DATA_PROJ,
          center: this.initCenter,  
          zoom: this.initZoom,
          minZoom: 0,
          maxZoom: 18,
          controls: { scaleLine: true, fullscreen: true, geolocation: true, zoom: true, attribution: true },
        }
      );

      // Add base layers      
      this.generateLayers();      

      this.map.on(MapEvents.Ready, () => {
        this.mapReadyAction.emit(true);
        this.map.activateHoverInfo();
      });
    } catch (error) {
      console.error('Error initializing map:', error);
    }
  }

  generateLayers() {
    this.map.addLayer(nbicMapPresets.osm);
    this.generateBackgroundMaps();
  }

  generateBackgroundMaps() {
    this.mapOverlays = [
      nbicMapPresets.osm,
      nbicMapPresets.topografiskBaseLayer,
      nbicMapPresets.nib,
      nbicMapPresets.topo4graatoneBaseLayer
    ];
    this.mapOverlays.forEach((layer) => {
      this.addLayer(layer)});
    this.mapConfig.wfsLayers?.forEach((wfs) => this.addLayer(wfs));
  }

  addLayer(layer:any){
    if (layer.source.type === 'wmts') {
      layer.source.options.projection = this.DATA_PROJ;
      layer.source.options.matrixSet = this.matrixSet;
    }else if(layer.source.type === 'wfs'){
      layer.source.options.srsName = this.DATA_PROJ;
    }else if(layer.source.type !== 'osm'){
      console.log("any other layers need to adjust projection?")
    }

    this.map.addLayer(layer)
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.destroy?.();
    }
  }
}
