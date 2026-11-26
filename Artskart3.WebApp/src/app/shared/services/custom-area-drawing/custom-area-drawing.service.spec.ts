import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomAreaDrawingCommand, CustomAreaDrawingService, MAX_CUSTOM_AREA_POINTS } from './custom-area-drawing.service';
import { FilterStateService } from '../filter-state/filter-state.service';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { ObservationSearchFilter } from '../../types/api.types';

const WKT = 'POLYGON((0 0,10 0,10 10,0 0))';
const OTHER_WKT = 'POLYGON((0 0,1 0,1 1,0 0))';
// One point too many, closing point included, as the API counts.
const FREEHAND_WKT = `POLYGON((${Array.from({ length: MAX_CUSTOM_AREA_POINTS }, (_, i) => `${i} ${i % 2}`).join(',')},0 0))`;

describe('CustomAreaDrawingService', () => {
  let service: CustomAreaDrawingService;
  let filterState: FilterStateService;
  let http: HttpTestingController;
  let commands: CustomAreaDrawingCommand[];
  // The real service resolves areas through AreaService, which loads the area list over HTTP.
  const searchFilter = signal<ObservationSearchFilter>({});

  beforeEach(() => {
    searchFilter.set({});
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SearchFilterService, useValue: { observationFilter: searchFilter } },
      ],
    });
    service = TestBed.inject(CustomAreaDrawingService);
    filterState = TestBed.inject(FilterStateService);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick();
    commands = [];
    service.commands.subscribe((command) => commands.push(command));
  });

  it('starter tegningen én gang', () => {
    service.start();
    service.start();

    expect(service.isDrawing()).toBe(true);
    expect(commands).toEqual(['start']);
  });

  it('kan ikke fullføres før polygonet har tre hjørner', () => {
    service.start();
    service.setPointCount(2);
    service.finish();

    expect(service.canFinish()).toBe(false);
    expect(commands).toEqual(['start']);

    service.setPointCount(3);
    service.finish();

    expect(commands).toEqual(['start', 'finish']);
  });

  it('avbryter og nullstiller', () => {
    service.start();
    service.setPointCount(4);
    service.cancel();

    expect(commands).toEqual(['start', 'cancel']);
    expect(service.isDrawing()).toBe(false);
    expect(service.canFinish()).toBe(false);
  });

  it('sender ingen kommandoer uten en tegning i gang', () => {
    service.undo();
    service.finish();
    service.cancel();

    expect(commands).toEqual([]);
  });

  afterEach(() => http.verify());

  const lookup = (wkt = WKT, filter: ObservationSearchFilter = {}) => {
    const request = http.expectOne('/api/Lookup/CustomArea');
    expect(request.request.body).toEqual({ wkt, filter });
    return request;
  };

  it('bruker polygonet som filter når det er innenfor grensen', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBe(WKT);
    expect(service.rejected()).toBeNull();
  });

  it('avviser et polygon med for mange lokaliteter og fjerner det forrige fra filteret', () => {
    filterState.setCustomArea('POLYGON((0 0,1 0,1 1,0 0))');

    service.submit(WKT);
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.rejected()).toEqual({ wkt: WKT, reason: 'tooManyLocations', limit: 200000 });
  });

  describe('når sjekken feiler', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it('prøver igjen og bruker polygonet når et senere forsøk lykkes', () => {
      service.submit(WKT);
      lookup().flush('feil', { status: 503, statusText: 'Service Unavailable' });
      vi.advanceTimersByTime(1000);
      lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

      expect(filterState.customAreaWkt()).toBe(WKT);
    });

    it('avviser polygonet etter tre mislykkede forsøk, og legger det ikke i filteret', () => {
      service.submit(WKT);
      for (let attempt = 1; attempt <= 2; attempt++) {
        lookup().flush('feil', { status: 429, statusText: 'Too Many Requests' });
        vi.advanceTimersByTime(1000);
      }
      lookup().flush('feil', { status: 500, statusText: 'Server Error' });

      expect(filterState.customAreaWkt()).toBeNull();
      expect(service.rejected()).toEqual({ wkt: WKT, reason: 'checkFailed', limit: undefined });
    });

    it('beholder et polygon som alt er i filteret når sjekken av det feiler', () => {
      filterState.setCustomArea(WKT);
      TestBed.tick();
      for (let attempt = 1; attempt <= 2; attempt++) {
        lookup().flush('feil', { status: 429, statusText: 'Too Many Requests' });
        vi.advanceTimersByTime(1000);
      }
      lookup().flush('feil', { status: 503, statusText: 'Service Unavailable' });

      expect(filterState.customAreaWkt()).toBe(WKT);
      expect(service.rejected()).toBeNull();
    });

    it('prøver ikke igjen når API-et avviser polygonet', () => {
      service.submit(WKT);
      lookup().flush('ugyldig', { status: 400, statusText: 'Bad Request' });
      vi.advanceTimersByTime(5000);

      expect(service.rejected()?.reason).toBe('checkFailed');
    });
  });

  it('avviser et polygon med for mange hjørner uten å spørre API-et', () => {
    filterState.setCustomArea(OTHER_WKT);

    service.submit(FREEHAND_WKT);

    http.expectNone('/api/Lookup/CustomArea');
    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.rejected()).toEqual({ wkt: FREEHAND_WKT, reason: 'tooManyCorners', limit: MAX_CUSTOM_AREA_POINTS });
  });

  it('godtar et avvist polygon når hjørnene er dratt innenfor grensen', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });
    service.submit(WKT);
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    expect(service.rejected()).toBeNull();
    expect(filterState.customAreaWkt()).toBe(WKT);
  });

  it('kan fjerne et avvist polygon', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });
    expect(service.hasArea()).toBe(true);

    service.remove();

    expect(service.rejected()).toBeNull();
    expect(service.hasArea()).toBe(false);
  });

  it('kan fjerne et polygon som er i filteret', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });
    expect(service.hasArea()).toBe(true);

    service.remove();

    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.hasArea()).toBe(false);
  });

  it('ser bort fra svaret på en sjekk av et polygon som er fjernet', () => {
    service.submit(WKT);
    service.remove();
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    searchFilter.set({ municipalityIds: ['0301'] });
    TestBed.tick();

    http.expectNone('/api/Lookup/CustomArea');
    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.rejected()).toBeNull();
  });

  it('ignorerer et svar som kommer etter at filteret er tømt', () => {
    service.submit(WKT);
    filterState.clearCustomArea();
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBeNull();
  });

  it('ignorerer et svar som kommer etter at et lagret filter er brukt', () => {
    service.submit(WKT);
    filterState.setCustomArea(OTHER_WKT);
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBe(OTHER_WKT);
    expect(service.rejected()).toBeNull();
  });

  it('glemmer et avvist polygon når filterets polygon byttes ut', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });
    filterState.setCustomArea(OTHER_WKT);

    expect(service.rejected()).toBeNull();
  });

  it('sjekker polygonet på nytt når andre områdefiltre legges til og fjernes', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    searchFilter.set({ municipalityIds: ['0301'] });
    TestBed.tick();
    lookup(WKT, { municipalityIds: ['0301'] }).flush({ tooManyLocations: true, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.rejected()).toEqual({ wkt: WKT, reason: 'tooManyLocationsWithAreas', limit: 200000 });

    searchFilter.set({});
    TestBed.tick();
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBe(WKT);
    expect(service.rejected()).toBeNull();
  });

  it('sender en sjekk som pågår på nytt når områdefiltrene endres', () => {
    service.submit(WKT);
    searchFilter.set({ municipalityIds: ['0301'] });
    TestBed.tick();

    const [first, second] = http.match('/api/Lookup/CustomArea');
    expect(first.cancelled).toBe(true);
    expect(second.request.body).toEqual({ wkt: WKT, filter: { municipalityIds: ['0301'] } });
    second.flush({ tooManyLocations: true, maxLocationCount: 200000 });

    expect(service.rejected()?.reason).toBe('tooManyLocationsWithAreas');
  });

  it('sjekker det nye polygonet, ikke det i filteret, når områdene endres under sjekken', () => {
    filterState.setCustomArea(OTHER_WKT);
    service.submit(WKT);
    searchFilter.set({ countyIds: ['03'] });
    TestBed.tick();

    const requests = http.match('/api/Lookup/CustomArea');
    expect(requests.map((r) => r.request.body.wkt)).toEqual([WKT, WKT]);
    requests[1].flush({ tooManyLocations: false, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBe(WKT);
  });

  it('sjekker et polygon som kommer fra et lagret filter', () => {
    filterState.setCustomArea(WKT);
    TestBed.tick();
    lookup().flush({ tooManyLocations: true, maxLocationCount: 200000 });

    expect(filterState.customAreaWkt()).toBeNull();
    expect(service.rejected()?.reason).toBe('tooManyLocations');
  });

  it('sjekker ikke på nytt etter at den selv har satt polygonet', () => {
    service.submit(WKT);
    lookup().flush({ tooManyLocations: false, maxLocationCount: 200000 });
    TestBed.tick();

    http.expectNone('/api/Lookup/CustomArea');
    expect(filterState.customAreaWkt()).toBe(WKT);
  });

  it('sjekker ikke uten et polygon når områdefiltrene endres', () => {
    searchFilter.set({ countyIds: ['03'] });
    TestBed.tick();

    http.expectNone('/api/Lookup/CustomArea');
  });
});
