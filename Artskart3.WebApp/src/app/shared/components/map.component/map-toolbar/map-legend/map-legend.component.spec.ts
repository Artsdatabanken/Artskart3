import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { MapLayerDto } from '@shared/types/api.types';
import { MapLegendComponent } from './map-legend.component';
import {
  BORDER_WIDTH,
  CLUSTER_MARKER_RADIUS,
  POLYGON_MARKER_SIZE,
  UNCLUSTERED_LOCATION_RADIUS,
  negativeGlyphPath,
  polygonIconBorderWidth,
  polygonIconPath,
} from '../../map-feature-styles';

const layer: MapLayerDto = {
  id: 1,
  name: 'Bioseksjoner',
  type: 'wms',
  url: 'https://example.test/wms?',
  layers: 'bioseksjoner',
  useSingleImage: false,
  retryAfterSeconds: 0,
  opacity: 1,
};

async function createLegend(inputs: { isOpen?: boolean; activeLayers?: MapLayerDto[]; geolocationActive?: boolean }) {
  await TestBed.configureTestingModule({ imports: [MapLegendComponent], providers: [provideTranslateService()] }).compileComponents();
  const fixture = TestBed.createComponent(MapLegendComponent);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('MapLegendComponent', () => {
  it('åpner og lukker med knappen, uavhengig av de andre menyene', async () => {
    const { fixture, element } = await createLegend({});
    const button = element.querySelector('adb-icon-button')!;

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('.map-legend-dropdown')).toBeNull();

    button.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('.map-legend-dropdown')).not.toBeNull();

    button.dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(element.querySelector('.map-legend-dropdown')).toBeNull();
  });

  it('tegner symbolene etter de samme målene som markørene i kartet', async () => {
    const { element } = await createLegend({ isOpen: true });
    const symbol = (index: number) => element.querySelectorAll('.map-legend-row')[index];

    expect(symbol(0).querySelector('.found-marker')!.getAttribute('r')).toBe(String(UNCLUSTERED_LOCATION_RADIUS));
    expect(symbol(1).querySelector('.negative-glyph')!.getAttribute('d')).toBe(
      negativeGlyphPath('absent', UNCLUSTERED_LOCATION_RADIUS, [
        Number(symbol(1).querySelector('.negative-marker')!.getAttribute('cx')),
        Number(symbol(1).querySelector('.negative-marker')!.getAttribute('cy')),
      ]),
    );
    expect(symbol(3).querySelector('.found-marker')!.getAttribute('r')).toBe(String(CLUSTER_MARKER_RADIUS));
    expect(symbol(3).querySelector('.found-marker')!.getAttribute('stroke-width')).toBe(String(BORDER_WIDTH));
    const polygon = symbol(4).querySelector('.polygon-marker')!;
    expect(polygon.getAttribute('d')).toBe(polygonIconPath());
    expect(Number(polygon.getAttribute('stroke-width'))).toBeCloseTo(polygonIconBorderWidth(POLYGON_MARKER_SIZE));
  });

  it('viser alltid markørtypene og clusterne når den er åpen', async () => {
    const { element } = await createLegend({ isOpen: true });

    const rows = [...element.querySelectorAll('.map-legend-row span')].map((label) => label.textContent?.trim());
    expect(rows).toEqual([
      'mapToolbar.legend.found',
      'mapToolbar.legend.absent',
      'mapToolbar.legend.notRecovered',
      'mapToolbar.legend.cluster',
      'mapToolbar.legend.polygonCluster',
    ]);
  });

  it('viser Din posisjon bare når posisjonen er aktivert', async () => {
    const { fixture, element } = await createLegend({ isOpen: true });
    expect(element.textContent).not.toContain('mapToolbar.legend.position');

    fixture.componentRef.setInput('geolocationActive', true);
    fixture.detectChanges();
    expect(element.textContent).toContain('mapToolbar.legend.position');
  });

  it('viser forklaringsbilde bare for aktive kartlag, og skjuler bilder som ikke lastes', async () => {
    const { fixture, element } = await createLegend({ isOpen: true });
    expect(element.querySelector('.map-legend-layer')).toBeNull();

    fixture.componentRef.setInput('activeLayers', [layer]);
    fixture.detectChanges();
    const image = element.querySelector<HTMLImageElement>('.map-legend-image')!;
    expect(new URL(image.src).searchParams.get('REQUEST')).toBe('GetLegendGraphic');
    expect(element.querySelector('.map-legend-layer-heading')?.textContent).toContain('mapToolbar.mapLayers.bioseksjoner');

    image.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(element.querySelector('.map-legend-layer')).toBeNull();
  });

  it('prøver igjen etter en mislykket lasting når laget skrus av og på, eller boksen åpnes på nytt', async () => {
    const { fixture, element } = await createLegend({ isOpen: true, activeLayers: [layer] });
    const failImage = () => {
      element.querySelector('.map-legend-image')!.dispatchEvent(new Event('error'));
      fixture.detectChanges();
    };

    failImage();
    expect(element.querySelector('.map-legend-image')).toBeNull();

    fixture.componentRef.setInput('activeLayers', []);
    fixture.detectChanges();
    fixture.componentRef.setInput('activeLayers', [layer]);
    fixture.detectChanges();
    expect(element.querySelector('.map-legend-image')).not.toBeNull();

    failImage();
    fixture.componentInstance.isOpen.set(false);
    fixture.detectChanges();
    fixture.componentInstance.isOpen.set(true);
    fixture.detectChanges();
    expect(element.querySelector('.map-legend-image')).not.toBeNull();
  });
});
