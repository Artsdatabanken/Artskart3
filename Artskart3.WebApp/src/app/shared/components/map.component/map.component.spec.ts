import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';

import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { MapComponent } from './map.component';
import { MapToolbarComponent } from './map-toolbar/map-toolbar.component';
import { ApiZoomLevel } from './map.types';

describe('MapComponent', () => {
  let component: MapComponent;
  let fixture: ComponentFixture<MapComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MapComponent, MapToolbarComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService()]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MapComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('applyGeoJsonToLayer', () => {
    let updateGeoJSONLayerSpy: ReturnType<typeof vi.fn>;
    const applyGeoJsonToLayer = (c: MapComponent, zoom: ApiZoomLevel, geojson: string) =>
      (c as unknown as { applyGeoJsonToLayer: (z: number, g: string) => void }).applyGeoJsonToLayer(zoom, geojson);

    beforeEach(() => {
      updateGeoJSONLayerSpy = vi.fn();
      component.map = { updateGeoJSONLayer: updateGeoJSONLayerSpy } as unknown as NbicMapComponent;
    });

    it('should route to counties layer for Counties zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.Counties, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-counties',
        '{"type":"FeatureCollection"}',
        { mode: 'replace' },
      );
    });

    it('should route to municipalities layer for Municipalities zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.Municipalities, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-municipalities',
        '{"type":"FeatureCollection"}',
        { mode: 'replace' },
      );
    });

    it('should route to locations layer with EPSG:4326 projection for LocationPoints zoom level', () => {
      applyGeoJsonToLayer(component, ApiZoomLevel.LocationPoints, '{"type":"FeatureCollection"}');

      expect(updateGeoJSONLayerSpy).toHaveBeenCalledWith(
        'area-markers-locations',
        '{"type":"FeatureCollection"}',
        { mode: 'replace', dataProjection: 'EPSG:4326' },
      );
    });

    it('should not call updateGeoJSONLayer when map is not set', () => {
      component.map = undefined as unknown as NbicMapComponent;

      applyGeoJsonToLayer(component, ApiZoomLevel.Counties, '{}');

      expect(updateGeoJSONLayerSpy).not.toHaveBeenCalled();
    });
  });
});
