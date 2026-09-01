import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { OrganizationService } from './organization.service';

describe('OrganizationService', () => {
  let service: OrganizationService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OrganizationService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  // Hvert av de tre typeahead-oppslagene har sitt eget endepunkt, fordi de
  // filtrerer på hver sin organisasjonstype. Treffer to av dem samme URL, får
  // brukeren samlinger i prosjektfeltet uten at noe feiler synlig.
  describe('endepunkter', () => {
    it('should call /api/Lookup/Organizations for searchOrganizations', () => {
      service.searchOrganizations('nina').subscribe();

      const req = httpTesting.expectOne(
        (r) => r.url === '/api/Lookup/Organizations' && r.params.get('search') === 'nina',
      );
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });

    it('should call /api/Lookup/Collections for searchCollections', () => {
      service.searchCollections('bergen').subscribe();

      const req = httpTesting.expectOne(
        (r) => r.url === '/api/Lookup/Collections' && r.params.get('search') === 'bergen',
      );
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });

    it('should call /api/Lookup/Datasets for searchDatasets', () => {
      service.searchDatasets('kartlegging').subscribe();

      const req = httpTesting.expectOne(
        (r) => r.url === '/api/Lookup/Datasets' && r.params.get('search') === 'kartlegging',
      );
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });

    it('should call /api/Lookup/CatalogNumbers for searchCatalogNumbers', () => {
      service.searchCatalogNumbers('1234').subscribe();

      const req = httpTesting.expectOne(
        (r) => r.url === '/api/Lookup/CatalogNumbers' && r.params.get('search') === '1234',
      );
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });
  });

  describe('svar', () => {
    it('should return organizations from searchCollections', async () => {
      const expected = [{ id: 26435, name: 'Aqua Kompetanse AS', code: 'AK', observationCount: 12 }];
      const result = service.searchCollections('aqua');
      const promise = new Promise((resolve) => result.subscribe(resolve));

      httpTesting.expectOne((r) => r.url === '/api/Lookup/Collections').flush(expected);

      expect(await promise).toEqual(expected);
    });

    // Katalognummer-treffet bærer ObservationId-ene med seg. Faller de bort,
    // sender filteret ingen ID-er og kartet viser alt ufiltrert.
    it('should return matches with observationIds from searchCatalogNumbers', async () => {
      const expected = [{ catalogNumber: '104168', observationIds: [8368071, 8368072] }];
      const result = service.searchCatalogNumbers('1041');
      const promise = new Promise((resolve) => result.subscribe(resolve));

      httpTesting.expectOne((r) => r.url === '/api/Lookup/CatalogNumbers').flush(expected);

      expect(await promise).toEqual(expected);
    });
  });
});
