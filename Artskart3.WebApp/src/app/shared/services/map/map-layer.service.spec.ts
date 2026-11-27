import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ApiClientService } from '@core/services/api-client.service';
import { MapLayerService } from './map-layer.service';

const layer = (id: number) => ({ id, name: `Lag ${id}`, type: 'wms', url: 'https://example.test/wms', useSingleImage: false, retryAfterSeconds: 0, opacity: 1 });

describe('MapLayerService', () => {
  it('retries loading the map layer list after an API failure', () => {
    let attempts = 0;
    TestBed.configureTestingModule({
      providers: [
        provideTranslateService(),
        {
          provide: ApiClientService,
          useValue: {
            fetchJson: () => (++attempts === 1 ? throwError(() => new Error('Unavailable')) : of([layer(1)])),
          },
        },
      ],
    });
    const service = TestBed.inject(MapLayerService);

    expect(service.mapLayers()).toEqual([]);
    expect(service.mapLayersLoadFailed()).toBe(true);

    service.retryMapLayersLoad();

    expect(attempts).toBe(2);
    expect(service.mapLayers().map((item) => item.id)).toEqual([1]);
    expect(service.mapLayersLoadFailed()).toBe(false);
  });

  it('lister påslåtte lag i samme rekkefølge som kartlagslisten, uansett hvilken rekkefølge de ble skrudd på', () => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService(), { provide: ApiClientService, useValue: { fetchJson: () => of([layer(1), layer(2), layer(3)]) } }],
    });
    const service = TestBed.inject(MapLayerService);

    service.setLayerEnabled(3, true);
    service.setLayerEnabled(1, true);
    expect(service.activeLayers().map((l) => l.id)).toEqual([1, 3]);

    service.setLayerEnabled(1, false);
    expect(service.activeLayers().map((l) => l.id)).toEqual([3]);
  });
});
