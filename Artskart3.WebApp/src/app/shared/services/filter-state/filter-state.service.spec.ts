import { TestBed } from '@angular/core/testing';
import { FilterStateService } from './filter-state.service';

describe('FilterStateService', () => {
  let service: FilterStateService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(FilterStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty selection', () => {
    expect(service.selectedCategoryIds()).toEqual([]);
    expect(service.selectedCountyIds()).toEqual([]);
    expect(service.selectedMunicipalityIds()).toEqual([]);
  });

  describe('toggleCategory', () => {
    it('should add category id when not selected', () => {
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([1]);
    });

    it('should remove category id when already selected', () => {
      service.toggleCategory(1);
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.toggleCategory(3);
      expect(service.selectedCategoryIds()).toEqual([1, 2, 3]);
    });

    it('should remove only the toggled id', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([2]);
    });
  });

  describe('clearCategories', () => {
    it('should reset selection to empty', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.clearCategories();
      expect(service.selectedCategoryIds()).toEqual([]);
    });
  });

  describe('toggleMunicipality', () => {
    it('should add municipality fid when not selected', () => {
      service.toggleMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0301']);
    });

    it('should remove municipality fid when already selected', () => {
      service.toggleMunicipality('0301');
      service.toggleMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleMunicipality('0301');
      service.toggleMunicipality('0602');
      expect(service.selectedMunicipalityIds()).toEqual(['0301', '0602']);
    });
  });

  describe('toggleCounty', () => {
    it('should add county fid when not selected', () => {
      service.toggleCounty('03');
      expect(service.selectedCountyIds()).toEqual(['03']);
    });

    it('should remove county fid when already selected', () => {
      service.toggleCounty('03');
      service.toggleCounty('03');
      expect(service.selectedCountyIds()).toEqual([]);
    });
  });

  describe('addMunicipality / removeMunicipality', () => {
    it('should add without duplicates', () => {
      service.addMunicipality('0301');
      service.addMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0301']);
    });

    it('should remove only the specified fid', () => {
      service.addMunicipality('0301');
      service.addMunicipality('0602');
      service.removeMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0602']);
    });
  });

  describe('clearAreas', () => {
    it('should reset both county and municipality selections to empty', () => {
      service.addCounty('03');
      service.addMunicipality('0301');
      service.clearAreas();
      expect(service.selectedCountyIds()).toEqual([]);
      expect(service.selectedMunicipalityIds()).toEqual([]);
    });
  });

  describe('clearAll', () => {
    it('should clear categories, counties, and municipalities', () => {
      service.toggleCategory(1);
      service.addCounty('03');
      service.addMunicipality('0301');
      service.clearAll();
      expect(service.selectedCategoryIds()).toEqual([]);
      expect(service.selectedCountyIds()).toEqual([]);
      expect(service.selectedMunicipalityIds()).toEqual([]);
    });
  });
});
