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

  it('ber om et utsnitt rundt punktet ut fra koordinatpresisjonen', () => {
    service.requestFitPoint(262000, 6650000, 8000);

    expect(service.consumeRequestedExtent()).toEqual([254000, 6642000, 270000, 6658000]);
  });

  it('viser minst 10 × 10 km rundt punktet', () => {
    service.requestFitPoint(262000, 6650000, null);

    expect(service.consumeRequestedExtent()).toEqual([257000, 6645000, 267000, 6655000]);
  });
});
