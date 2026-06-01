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
    expect(service.selectedAreaIds()).toEqual([]);
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

  describe('toggleArea', () => {
    it('should add area fid when not selected', () => {
      service.toggleArea('0301');
      expect(service.selectedAreaIds()).toEqual(['0301']);
    });

    it('should remove area fid when already selected', () => {
      service.toggleArea('0301');
      service.toggleArea('0301');
      expect(service.selectedAreaIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleArea('03');
      service.toggleArea('0301');
      expect(service.selectedAreaIds()).toEqual(['03', '0301']);
    });
  });

  describe('addArea / removeArea', () => {
    it('should add without duplicates', () => {
      service.addArea('03');
      service.addArea('03');
      expect(service.selectedAreaIds()).toEqual(['03']);
    });

    it('should remove only the specified fid', () => {
      service.addArea('03');
      service.addArea('0301');
      service.removeArea('03');
      expect(service.selectedAreaIds()).toEqual(['0301']);
    });
  });

  describe('clearAreas', () => {
    it('should reset area selection to empty', () => {
      service.addArea('03');
      service.addArea('0301');
      service.clearAreas();
      expect(service.selectedAreaIds()).toEqual([]);
    });
  });

  describe('clearAll', () => {
    it('should clear both categories and areas', () => {
      service.toggleCategory(1);
      service.addArea('03');
      service.clearAll();
      expect(service.selectedCategoryIds()).toEqual([]);
      expect(service.selectedAreaIds()).toEqual([]);
    });
  });
});
