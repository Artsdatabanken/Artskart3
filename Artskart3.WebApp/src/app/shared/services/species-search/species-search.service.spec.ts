import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { SpeciesSearchService } from './species-search.service';
import { SpeciesDto } from '../../types/api.types';

describe('SpeciesSearchService', () => {
  let service: SpeciesSearchService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SpeciesSearchService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call the species search endpoint with the search param', () => {
    const mockResults: SpeciesDto[] = [
      {
        taxonId: 1,
        scientificName: 'Parus major',
        author: 'Linnaeus, 1758',
        preferredVernacularNames: [{ name: 'Kjøttmeis', language: 'nb' }],
      },
    ];

    service.searchSpecies('kjøtt').subscribe((results) => {
      expect(results).toEqual(mockResults);
    });

    const req = httpTesting.expectOne('/api/Search/Species?search=kj%C3%B8tt');
    expect(req.request.method).toBe('GET');
    req.flush(mockResults);
  });
});
