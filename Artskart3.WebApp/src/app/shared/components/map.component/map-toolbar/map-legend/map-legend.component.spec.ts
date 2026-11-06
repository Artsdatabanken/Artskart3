import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { MapLayerDto } from '@shared/types/map-layer.types';
import { MapLegendComponent } from './map-legend.component';

const layer: MapLayerDto = { id: 1, name: 'Bioseksjoner', type: 'wms', url: 'https://example.test/wms?', layers: 'bioseksjoner' };

async function createLegend(inputs: { isOpen?: boolean; activeLayers?: MapLayerDto[]; geolocationActive?: boolean }) {
  await TestBed.configureTestingModule({ imports: [MapLegendComponent], providers: [provideTranslateService()] }).compileComponents();
  const fixture = TestBed.createComponent(MapLegendComponent);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('MapLegendComponent', () => {
  it('åpner og lukker med knappen', async () => {
    const { fixture, element } = await createLegend({});
    const opened: boolean[] = [];
    fixture.componentInstance.mapLegendOpenChange.subscribe((open) => opened.push(open));
    const button = element.querySelector('adb-icon-button')!;

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('.map-legend-dropdown')).toBeNull();

    button.dispatchEvent(new Event('click'));
    expect(opened).toEqual([true]);
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
});
