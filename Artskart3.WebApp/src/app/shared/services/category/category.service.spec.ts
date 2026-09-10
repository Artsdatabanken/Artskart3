import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CategoryService } from './category.service';
import { CategoryTypeDto } from '../../types/api.types';

describe('CategoryService', () => {
  let service: CategoryService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CategoryService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
    // Tjenesten henter kategoriene ved opprettelse (delt strøm).
    httpTesting.expectOne('/api/Lookup/Categories').flush([]);
  });

  it('should call GET /api/Lookup/Categories', () => {
    const mockResponse: CategoryTypeDto[] = [
      { id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }] },
      { id: 2, name: 'Fremmedart', categories: [{ id: 7, code: 'SE', name: 'Svært høy risiko' }] },
    ];

    service.getCategories().subscribe((result) => {
      expect(result).toEqual(mockResponse);
    });

    const req = httpTesting.expectOne('/api/Lookup/Categories');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('deler én request mellom abonnenter', () => {
    service.getCategories().subscribe();
    service.getCategories().subscribe();
    httpTesting.expectOne('/api/Lookup/Categories').flush([]);
  });

  it('bygger categoryTypeNameById fra kategoriene', () => {
    httpTesting.expectOne('/api/Lookup/Categories').flush([
      { id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }] },
      { id: 2, name: 'Fremmedart', categories: [{ id: 7, code: 'SE', name: 'Svært høy risiko' }] },
    ]);
    const map = service.categoryTypeNameById();
    expect(map.get(10)).toBe('Rødliste');
    expect(map.get(7)).toBe('Fremmedart');
  });
});
