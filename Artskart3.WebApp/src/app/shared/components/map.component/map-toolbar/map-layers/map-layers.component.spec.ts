import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ApiClientService } from '@core/services/api-client.service';
import { MapLayerService } from '@shared/services/map/map-layer.service';
import { AlertService } from '@shared/services/alert/alert.service';
import { MapLayersComponent } from './map-layers.component';

describe('MapLayersComponent load warnings', () => {
  it('shows inline alerts under failed enabled layers and keeps the global warning when the menu closes', async () => {
    const layers = [
      { id: 1, name: 'First layer', type: 'wms', url: 'https://example.test/first' },
      { id: 2, name: 'Second layer', type: 'wms', url: 'https://example.test/second' },
    ];
    await TestBed.configureTestingModule({
      imports: [MapLayersComponent],
      providers: [provideTranslateService(), { provide: ApiClientService, useValue: { fetchJson: () => of(layers) } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(MapLayersComponent);
    fixture.componentRef.setInput('isOpen', true);
    fixture.componentInstance.enabledLayerIds.set(new Set([1, 2]));
    const service = TestBed.inject(MapLayerService);
    const alerts = TestBed.inject(AlertService);
    const element: HTMLElement = fixture.nativeElement;
    fixture.detectChanges();
    const toggleButton = element.querySelector('adb-icon-button')!;
    expect(toggleButton.getAttribute('variant')).toBe('primary');
    expect(toggleButton.getAttribute('aria-pressed')).toBe('true');
    expect(element.querySelector('adb-alert')).toBeNull();
    service.setLayerFailed(1, true);
    fixture.detectChanges();
    const inlineAlert = element.querySelector('adb-alert')!;
    expect(element.querySelectorAll('adb-alert')).toHaveLength(1);
    expect(inlineAlert.previousElementSibling?.textContent).toContain('First layer');
    expect(inlineAlert.getAttribute('variant')).toBe('warning');
    expect(inlineAlert.getAttribute('type')).toBe('inline');
    expect(inlineAlert.getAttribute('size')).toBe('sm');
    expect(inlineAlert.getAttribute('heading')).toBe('mapToolbar.layerLoadError');
    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();
    expect(toggleButton.getAttribute('variant')).toBe('secondary');
    expect(toggleButton.getAttribute('aria-pressed')).toBe('false');
    expect(element.querySelector('adb-alert')).toBeNull();
    expect(alerts.visibleAlerts()).toHaveLength(1);
    service.setLayerFailed(1, false);
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    expect(element.querySelector('adb-alert')).toBeNull();
    expect(alerts.visibleAlerts()).toHaveLength(0);
    fixture.destroy();
  });
});
