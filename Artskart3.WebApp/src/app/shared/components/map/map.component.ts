import { createMap, MapEventPayload, MapEvents, nbicMapPresets } from 'nbic-map-component';
import { AfterViewInit, Component, ElementRef, Output, EventEmitter, ViewChild, OnDestroy } from '@angular/core';
import { BuildOptions, Feature, FeatureCollection, GeoJsonProperties, Geometry, MapConfig, Site } from '../../models/map.model';
import {  MapService} from './map.service';
import {
  NbicMapComponent,
  LayerDef,
  nbicMapGeojson,
  nbicMapUtils,
} from 'nbic-map-component';
@Component({
  selector: 'app-map',
  standalone: false,
  templateUrl: './map.component.html',
  styleUrl: './map.component.css',
})
export class MapComponent implements AfterViewInit, OnDestroy {
 
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

  constructor(
    private mapService: MapService,
  ) {}

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
          projection: this.mapService.DATA_PROJ,
          center: this.mapService.initCenter,  
          zoom: this.mapService.initZoom,
          minZoom: 0,
          maxZoom: 18,
          controls: { scaleLine: true, fullscreen: true, geolocation: true, zoom: true, attribution: true },
        }
      );

      this.generateLayers();      
      if (this.mapConfig.controls.mapClick) {
        this.map.on(MapEvents.PointerClick, this.mapClickAction);
      }

      this.map.on(MapEvents.Ready, () => {
        this.mapReadyAction.emit(true);
        this.map.activateHoverInfo();
      });
      this.addData();
      
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
      layer.source.options.projection = this.mapService.DATA_PROJ;
      layer.source.options.matrixSet = this.mapService.matrixSet;
    }else if(layer.source.type === 'wfs'){
      layer.source.options.srsName = this.mapService.DATA_PROJ;
    }else if(layer.source.type !== 'osm'){
      console.log("any other layers need to adjust projection?")
    }
    this.map.addLayer(layer)
  }

  addData(){
    const data = this.mapService.getData();
    console.log("add data:", data)
  }


  ngOnDestroy(): void {
    if (this.map) {
      this.map.destroy?.();
    }
  }

  private mapClickAction = (
    event: MapEventPayload<typeof MapEvents.PointerClick>
  ) => {
  
    const clickedFeatures = event?.features ?? [];    
    
    if (clickedFeatures.length > 0) {
      this.handleClickedFeatures(clickedFeatures);
    } else{
      console.log("clicked the empty spot with coordinates: ", event.clickCoordinate );
      this.makeNewClickedFeature(event)
    }
  };

  handleClickedFeatures(clickedFeatures:any){
    console.log("CLICKED THESE FEATURES", clickedFeatures)
  }

  makeNewClickedFeature(event: MapEventPayload<typeof MapEvents.PointerClick>) {
    console.log("make new click feature")
    this.map.setLayerVisibility('draw-layer', true);
    const newSite = this.mapService.initNewSite(event.clickCoordinate);
    const transformedCoord = this.map.transformCoordsFrom(
      event.clickCoordinate,
      this.mapService.VIEW_PROJ,
      this.mapService.DATA_PROJ
    )
    newSite.geometry.coordinates = transformedCoord; 

    this.mapService.updateActiveSite(newSite);
      
  }


}
