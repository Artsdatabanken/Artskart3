import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AreaService } from './area.service';
import { AreaResponseDto } from '../../types/api.types';
import { FilterStateService } from '../filter-state/filter-state.service';

describe('AreaService', () => {
  let service: AreaService;
  let httpTesting: HttpTestingController;
  let filterState: FilterStateService;

  const mockAreaResponse: AreaResponseDto = {
    counties: {
      fastlandsNorge: [
        { id: 1, fid: '03', name: 'Oslo', isCurrent: true, observationCount: 100 },
        { id: 2, fid: '11', name: 'Rogaland', isCurrent: true, observationCount: 200 },
      ],
    },
    municipalities: {
      id: 2,
      name: 'Municipality',
      areas: [
        { id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true, observationCount: 100 },
        { id: 11, fid: '1101', name: 'Eigersund', isCurrent: true, observationCount: 50 },
        { id: 12, fid: '1103', name: 'Stavanger', isCurrent: true, observationCount: 150 },
      ],
    },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AreaService);
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  function flushAreas() {
    httpTesting.expectOne('/api/Lookup/Areas').flush(mockAreaResponse);
    TestBed.flushEffects();
  }

  it('should be created', () => {
    expect(service).toBeTruthy();
    httpTesting.expectOne('/api/Lookup/Areas');
  });

  it('should expose counties from the response', () => {
    flushAreas();
    const counties = service.counties();
    expect(counties.length).toBe(2);
    expect(counties[0].name).toBe('Oslo');
  });

  it('should expose municipalities from the response', () => {
    flushAreas();
    const municipalities = service.municipalities();
    expect(municipalities.length).toBe(3);
    expect(municipalities[0].name).toBe('Oslo kommune');
  });

  it('should compute county groups', () => {
    flushAreas();
    const groups = service.countyGroups();
    expect(groups.length).toBe(2);
    expect(groups[0].county.name).toBe('Oslo');
    expect(groups[0].municipalities.length).toBe(1);
    expect(groups[1].county.name).toBe('Rogaland');
    expect(groups[1].municipalities.length).toBe(2);
  });

  it('should resolve area filter with county when all municipalities selected', () => {
    flushAreas();
    filterState.addMunicipality('0301');
    TestBed.flushEffects();

    const resolved = service.resolvedAreaFilter();
    expect(resolved.countyIds).toEqual(['03']);
    expect(resolved.municipalityIds).toEqual([]);
  });

  it('should resolve area filter with municipality IDs when only some selected', () => {
    flushAreas();
    filterState.addMunicipality('1101');
    TestBed.flushEffects();

    const resolved = service.resolvedAreaFilter();
    expect(resolved.countyIds).toEqual([]);
    expect(resolved.municipalityIds).toEqual(['1101']);
  });

  it('should return empty arrays when areas endpoint fails', () => {
    httpTesting.expectOne('/api/Lookup/Areas').error(new ProgressEvent('error'));
    TestBed.flushEffects();

    expect(service.counties()).toEqual([]);
    expect(service.municipalities()).toEqual([]);
    expect(service.resolvedAreaFilter()).toEqual({ countyIds: [], municipalityIds: [] });
  });
});
