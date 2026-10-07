import { TestBed } from '@angular/core/testing';
import { MapViewService } from './map-view.service';

describe('MapViewService', () => {
  let service: MapViewService;

  beforeEach(() => {
    service = TestBed.inject(MapViewService);
  });

  it('gir det forespurte utsnittet én gang', () => {
    service.requestFit([1, 2, 3, 4]);

    expect(service.consumeRequestedExtent()).toEqual([1, 2, 3, 4]);
    expect(service.consumeRequestedExtent()).toBeNull();
    expect(service.requestedExtent()).toBeNull();
  });
});
