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
});
