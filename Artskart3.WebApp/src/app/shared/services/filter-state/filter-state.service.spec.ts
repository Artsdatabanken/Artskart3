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
});
