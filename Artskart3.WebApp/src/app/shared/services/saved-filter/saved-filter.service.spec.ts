import { TestBed } from '@angular/core/testing';
import { ApplicationRef, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { SavedFilterService } from './saved-filter.service';
import { AuthService } from '../auth/auth.service';
import { MapViewService } from '../map-view/map-view.service';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { SavedFilterDto } from '../../types/api.types';

describe('SavedFilterService', () => {
  let service: SavedFilterService;
  let httpTesting: HttpTestingController;
  let mapView: MapViewService;
  const isAuthenticated = signal(true);
  const searchFilter = {
    observationFilter: signal({ taxonIds: [1] }),
    applyFilter: vi.fn().mockResolvedValue(undefined),
  };

  const SAVED: SavedFilterDto[] = [
    { id: 'a', name: 'Fugler', filter: { taxonGroupIds: [1] }, isDefault: false, createdAt: '2026-01-01T00:00:00Z' },
    {
      id: 'b',
      name: 'Pattedyr',
      filter: { taxonGroupIds: [2] },
      extent: { minX: 1, minY: 2, maxX: 3, maxY: 4 },
      isDefault: true,
      createdAt: '2026-01-02T00:00:00Z',
    },
  ];

  beforeEach(() => {
    isAuthenticated.set(true);
    searchFilter.applyFilter.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated } },
        { provide: SearchFilterService, useValue: searchFilter },
      ],
    });
    service = TestBed.inject(SavedFilterService);
    mapView = TestBed.inject(MapViewService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  async function flushList(filters: SavedFilterDto[] = SAVED): Promise<void> {
    TestBed.tick();
    httpTesting.expectOne('/api/User/SavedFilters').flush(filters);
    await TestBed.inject(ApplicationRef).whenStable();
  }

  it('henter ikke filtre for anonyme brukere', () => {
    isAuthenticated.set(false);
    TestBed.tick();

    httpTesting.expectNone('/api/User/SavedFilters');
    expect(service.filters()).toEqual([]);
  });

  it('finner standardfilteret', async () => {
    await flushList();

    expect(service.defaultFilter()?.id).toBe('b');
  });

  it('lagrer gjeldende filter og kartutsnitt', async () => {
    await flushList();
    mapView.currentExtent.set([10, 20, 30, 40]);

    const created = firstValueFrom(service.create('Nytt', true));
    const req = httpTesting.expectOne({ method: 'POST', url: '/api/User/SavedFilters' });
    expect(req.request.body).toEqual({
      name: 'Nytt',
      filter: { taxonIds: [1] },
      extent: { minX: 10, minY: 20, maxX: 30, maxY: 40 },
      isDefault: true,
    });
    req.flush(SAVED[0]);
    await created;

    TestBed.tick();
    httpTesting.expectOne('/api/User/SavedFilters').flush(SAVED);
  });

  it('lagrer uten utsnitt når kartet ikke har vært tegnet', async () => {
    await flushList();

    service.create('Uten kart', false).subscribe();

    const req = httpTesting.expectOne({ method: 'POST', url: '/api/User/SavedFilters' });
    expect(req.request.body.extent).toBeUndefined();
    req.flush(SAVED[0]);
    TestBed.tick();
    httpTesting.expectOne('/api/User/SavedFilters').flush(SAVED);
  });

  it('aktivering setter filteret og ber kartet zoome til utsnittet', async () => {
    await service.activate(SAVED[1]);

    expect(searchFilter.applyFilter).toHaveBeenCalledWith({ taxonGroupIds: [2] });
    expect(mapView.requestedExtent()).toEqual([1, 2, 3, 4]);
    await flushList();
  });

  it('aktivering uten utsnitt lar kartet stå', async () => {
    await service.activate(SAVED[0]);

    expect(mapView.requestedExtent()).toBeNull();
    await flushList();
  });

  it('findById gir null for et filter som ikke finnes', async () => {
    await flushList();

    const result = firstValueFrom(service.findById('finnes-ikke'));
    httpTesting.expectOne('/api/User/SavedFilters').flush(SAVED);

    expect(await result).toBeNull();
  });
});
