import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { FacetCountService, FacetCountsResponse } from './facet-count.service';

/**
 * Tester for når tjenesten henter, og for at et manglende tall betyr null treff.
 *
 * Debouncen er 200 ms, så testene venter med ekte tid framfor fakeAsync: `toObservable`
 * sender først ved neste mikrotask, og det er lettere å lese enn en stabel med tick().
 */
describe('FacetCountService', () => {
  let service: FacetCountService;
  let httpMock: HttpTestingController;

  const svar: FacetCountsResponse = {
    dimensions: [{ key: 'taxonGroup', source: 'cache', buckets: [{ id: 8, count: 42 }] }],
    boolFilters: [{ key: 'withImages', whenTrue: 5, whenFalse: 95 }],
    areas: [{ fid: '50', count: 7976997 }],
  };

  const settle = () => new Promise((resolve) => setTimeout(resolve, 300));

  /** Bare kall til fasett-endepunktet; andre tjenester har sine egne. */
  const facetRequests = () => httpMock.match((r) => r.url === '/api/Search/FacetCounts');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Den ekte SearchFilterService drar inn AreaService, som laster områdelista
        // over HTTP ved oppstart. Her er filteret inndata, ikke noe som testes.
        { provide: SearchFilterService, useValue: { observationFilter: signal({}) } },
      ],
    });
    service = TestBed.inject(FacetCountService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('henter ingenting når ingen grupper er åpne', async () => {
    await settle();

    expect(facetRequests()).toHaveLength(0);
  });

  it('henter når en gruppe åpnes, og bare for den', async () => {
    service.setDimensionOpen('taxonGroup', true);
    await settle();

    const req = httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts');
    expect(req.request.params.get('dimensions')).toBe('taxonGroup');
    expect(req.request.params.has('boolFilters')).toBe(false);
    req.flush(svar);
  });

  it('samler flere åpne grupper i ett kall', async () => {
    service.setDimensionOpen('taxonGroup', true);
    service.setDimensionOpen('category', true);
    service.setBoolGroupOpen('withImages', true);
    await settle();

    const req = httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts');
    expect(req.request.params.get('dimensions')).toBe('category,taxonGroup');
    expect(req.request.params.get('boolFilters')).toBe('withImages');
    req.flush(svar);
  });

  it('slutter å hente når gruppen lukkes', async () => {
    service.setDimensionOpen('taxonGroup', true);
    await settle();
    httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts').flush(svar);

    service.setDimensionOpen('taxonGroup', false);
    await settle();

    expect(facetRequests()).toHaveLength(0);
  });

  it('gjør svaret om til oppslag per bøtte', async () => {
    service.setDimensionOpen('taxonGroup', true);
    await settle();
    httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts').flush(svar);

    expect(service.hasCountsFor('taxonGroup')).toBe(true);
    expect(service.countsFor('taxonGroup').get(8)).toBe(42);

    // En bøtte som ikke står i svaret har null treff — serveren utelater dem.
    expect(service.countsFor('taxonGroup').get(19)).toBeUndefined();

    expect(service.boolCountFor('withImages', true)).toBe(5);
    expect(service.boolCountFor('withImages', false)).toBe(95);
  });

  it('henter omraadelistene naar omraadegruppen aapnes', async () => {
    service.setAreasOpen(true);
    await settle();

    const req = httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts');
    expect(req.request.params.get('includeAreas')).toBe('true');
    req.flush(svar);

    expect(service.areaCountFor('50')).toBe(7976997);

    // Et omraade som ikke staar i svaret har null treff, ikke ukjent antall.
    expect(service.areaCountFor('42')).toBe(0);
  });

  it('har ingen omraadetall foer listene er hentet', () => {
    expect(service.areaCountFor('50')).toBeNull();
  });

  it('har ingen tall for en gruppe som ikke er hentet', () => {
    expect(service.hasCountsFor('category')).toBe(false);
    expect(service.countsFor('category').size).toBe(0);
    expect(service.boolCountFor('sensitive', true)).toBeNull();
  });

  /**
   * Et feilet kall skal ikke blanke panelet. Tallene er en hjelp, ikke funksjonalitet
   * brukeren er avhengig av.
   */
  it('beholder forrige svar når et kall feiler', async () => {
    service.setDimensionOpen('taxonGroup', true);
    await settle();
    httpMock.expectOne((r) => r.url === '/api/Search/FacetCounts').flush(svar);

    service.setDimensionOpen('category', true);
    await settle();
    httpMock
      .expectOne((r) => r.url === '/api/Search/FacetCounts')
      .flush('feil', { status: 500, statusText: 'Server Error' });

    expect(service.countsFor('taxonGroup').get(8)).toBe(42);
  });
});
