import { Injectable } from '@angular/core';
import { FeatureCollection } from 'geojson';
import {
  BehaviorSubject,
  Observable,
  tap,
  catchError,
  Subject,
  combineLatest,
  map
} from 'rxjs';
import {
  NbicMapComponent,
  nbicMapUtils,
  nbicMapGeojson
} from 'nbic-map-component';
import { Feature } from 'ol';
import { Geometry, Point, Polygon } from 'ol/geom';
import { Site } from '../../models/map.model';

type GeoJsonVectorLayerArgs = {
    id: string;
    fc: FeatureCollection;
    visible?: boolean;
    style?: any;
    cluster?: any;
    hover?: any;
    zIndex?: number;
    zIndexPinned?: boolean;
  };
  
@Injectable({
    providedIn: 'root'
  })
export class MapService {
    readonly DATA_PROJ = 'EPSG:25833' as const;
    readonly VIEW_PROJ = 'EPSG:3857' as const; // is it tho? who knows.    
    readonly matrixSet = 'utm33n' as const;
    readonly initZoom = 6.2 as const;
    readonly initCenter: [number, number] =  [300000, 7220000] as const; // Centered on Norway in UTM 33N  
    private searchFeatureCollection =
    new BehaviorSubject<FeatureCollection | null>(null);
    searchFeaturesChangedAction$ = this.searchFeatureCollection.asObservable();
    
  private activeSite = new BehaviorSubject<Site | null>(null);
  activeSiteChangedAction$ = this.activeSite.asObservable();

    constructor(
        
      ) {}
    

    setSearchFeatureCollection(sites: FeatureCollection | null) {
        this.searchFeatureCollection.next(sites);
    }

    getSearchFeatureCollection(): any {
        return this.searchFeatureCollection.value;
    }

    cleanup() {
        this.setSearchFeatureCollection(null);
    }

    getData(){
        const data = this.useTestData();
        data.forEach((element:Site) => {
            console.log(element)            
        }); 
        return data;

    }

    makeFeatureCollectionFromSites(
        sites: Site[]
      )
      {
        console.log("received this: ", sites)
        const site = sites[0];
        const opts = {
          kind: 'Point',

        }

        const features = sites
        .map((item, i) => this.toFeature(item, i, opts))
        //.filter((f): f is Feature<Geometry, GeoJsonProperties> => (filterNull ? !!f : true));

        let fx = { type: 'FeatureCollection', features }
        //site,count: site.properties.ObservationCount
        const properties = site.properties;

        
        console.log(site.geometry)
        const fc = nbicMapGeojson.toFeatureCollection(sites, {
          kind: 'Point',         
          getPoint: (site: Site) => ({
            lon: site.geometry.coordinates[1] || 0,
            lat: site.geometry.coordinates[0]  || 0
          }),
          props: (site: Site) => ({site,count: site.properties.ObservationCount})
        });

        console.log("FEATURECOLLECTION:",fc)
        return fc;
      }

      toFeature(item:Site, i:number, opts:any){
        console.log("FFS THESE OPTS")

      }
    

    useTestData =():any=>{
        return  [ {"geometry": { "coordinates": [-62453.0, 6662901.0], "type": "Point" }, "id": "2975212", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }]
        //return { "features": [{ "geometry": { "coordinates": [-62453.0, 6662901.0], "type": "Point" }, "id": "2975212", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [269109.0, 6785956.9], "type": "Point" }, "id": "4987698", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [312976.0, 7134167.0], "type": "Point" }, "id": "2982186", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [190854.7, 6871317.1], "type": "Point" }, "id": "5598490", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [107521.0, 6789641.0], "type": "Point" }, "id": "2982208", "properties": { "ObservationCount": 4, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [991554.6, 7826503.5], "type": "Point" }, "id": "4987702", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [711009.7, 7690326.6], "type": "Point" }, "id": "4987694", "properties": { "ObservationCount": 2, "MaxCategory": 0 }, "type": "Feature" }, { "geometry": { "coordinates": [513685.5, 8681876.0], "type": "Point" }, "id": "4991483", "properties": { "ObservationCount": 1, "MaxCategory": 0 }, "type": "Feature" }, { "geometry": { "coordinates": [298361.4, 7159407.1], "type": "Point" }, "id": "4987705", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [521243.0, 8648074.0], "type": "Point" }, "id": "4991484", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [712054.0, 7689358.0], "type": "Point" }, "id": "2975257", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [887491.6, 7919339.0], "type": "Point" }, "id": "4987700", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [-37941.5, 6719564.9], "type": "Point" }, "id": "4987690", "properties": { "ObservationCount": 3, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [666242.3, 7712492.7], "type": "Point" }, "id": "4987691", "properties": { "ObservationCount": 17, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [653330.2, 7729941.6], "type": "Point" }, "id": "4987701", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [44889.0, 6740699.0], "type": "Point" }, "id": "2975131", "properties": { "ObservationCount": 4, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [433245.6, 8762419.3], "type": "Point" }, "id": "4991488", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [220513.5, 6919602.7], "type": "Point" }, "id": "4987178", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [476681.0, 7570985.0], "type": "Point" }, "id": "2975268", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [251438.0, 6833807.0], "type": "Point" }, "id": "2975113", "properties": { "ObservationCount": 15, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [288511.0, 7162534.0], "type": "Point" }, "id": "4609758", "properties": { "ObservationCount": 15, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [297192.1, 7153897.6], "type": "Point" }, "id": "4987699", "properties": { "ObservationCount": 9, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [305638.7, 7154104.6], "type": "Point" }, "id": "4987692", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [293314.5, 7156020.6], "type": "Point" }, "id": "4987704", "properties": { "ObservationCount": 4, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [149917.0, 6804541.0], "type": "Point" }, "id": "4607075", "properties": { "ObservationCount": 5, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [1019450.0, 7839837.0], "type": "Point" }, "id": "2975155", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [367530.0, 7146313.9], "type": "Point" }, "id": "4987703", "properties": { "ObservationCount": 6, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [974705.0, 7842296.0], "type": "Point" }, "id": "2975197", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [432555.4, 8762380.0], "type": "Point" }, "id": "4991486", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [535471.7, 7597757.4], "type": "Point" }, "id": "4987693", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [432623.9, 8762475.0], "type": "Point" }, "id": "4991487", "properties": { "ObservationCount": 1, "MaxCategory": 12 }, "type": "Feature" }, { "geometry": { "coordinates": [46018.1, 6737594.2], "type": "Point" }, "id": "4987696", "properties": { "ObservationCount": 6, "MaxCategory": 11 }, "type": "Feature" }, { "geometry": { "coordinates": [471519.6, 8667288.0], "type": "Point" }, "id": "4991485", "properties": { "ObservationCount": 3, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [852018.6, 7896295.9], "type": "Point" }, "id": "4987707", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [282487.5, 6690859.5], "type": "Point" }, "id": "5598489", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [384269.0, 7156530.0], "type": "Point" }, "id": "2975175", "properties": { "ObservationCount": 10, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [305199.6, 7153375.6], "type": "Point" }, "id": "4984505", "properties": { "ObservationCount": 4, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [-38661.2, 6665857.1], "type": "Point" }, "id": "4171362", "properties": { "ObservationCount": 1, "MaxCategory": 12 }, "type": "Feature" }, { "geometry": { "coordinates": [717131.4, 7688983.3], "type": "Point" }, "id": "4171363", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [355757.1, 7152426.2], "type": "Point" }, "id": "4987706", "properties": { "ObservationCount": 3, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [42167.1, 6806942.4], "type": "Point" }, "id": "4171361", "properties": { "ObservationCount": 3, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [1067339.0, 7800189.0], "type": "Point" }, "id": "2975163", "properties": { "ObservationCount": 2, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [432425.0, 8762480.1], "type": "Point" }, "id": "4991489", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [384836.0, 7121970.0], "type": "Point" }, "id": "2975200", "properties": { "ObservationCount": 8, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [220532.0, 6919850.1], "type": "Point" }, "id": "4987695", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [523018.0, 7419366.0], "type": "Point" }, "id": "2975122", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [180124.0, 6852570.0], "type": "Point" }, "id": "2975159", "properties": { "ObservationCount": 8, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [368805.8, 7484789.8], "type": "Point" }, "id": "4987708", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [322018.8, 7152347.7], "type": "Point" }, "id": "4987697", "properties": { "ObservationCount": 1, "MaxCategory": 11 }, "type": "Feature" }, { "geometry": { "coordinates": [250133.0, 7043517.0], "type": "Point" }, "id": "4607074", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }, { "geometry": { "coordinates": [210762.0, 6895320.0], "type": "Point" }, "id": "2975107", "properties": { "ObservationCount": 1, "MaxCategory": 9 }, "type": "Feature" }], "crs": { "properties": { "name": "EPSG:32633" }, "type": "Name" }, "type": "FeatureCollection" }
    }

 
  
  initNewSite(coordinate: number[], name: string = ''): Site {
    const site: Site = {
      id: 0,
      geometry:{
        type:"point",
        coordinates:coordinate
      },
      properties:{
        ObservationCount: 0, 
        MaxCategory: 0
      },
      type: "feature"
    };
    return site;
  }

  
  updateActiveSite(site: Site | null): void {
    console.log("UPDATE ACTIVE SITE", site)
    if (site !== null && this.activeSite.value === site) {
      console.info('site is active site, not updating.');
      return;
    }
    if (site){
    this.activeSite.next(site);
      this.makeFeatureCollectionFromSites([site])
    }
    
  }
      
}