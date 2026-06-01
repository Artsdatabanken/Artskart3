import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AreaService } from './area.service';
import { AreaTypeDto } from '../../types/api.types';

describe('AreaService', () => {
  let service: AreaService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AreaService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch areas from the API', () => {
    const mockAreas: AreaTypeDto[] = [
      { id: 1, name: 'County', areas: [{ id: 1, fid: '03', name: 'Oslo', isCurrent: true }] },
    ];

    service.getAreas().subscribe((areas) => {
      expect(areas).toEqual(mockAreas);
    });

    const req = httpTesting.expectOne('/api/Lookup/Areas');
    expect(req.request.method).toBe('GET');
    req.flush(mockAreas);
  });
});
