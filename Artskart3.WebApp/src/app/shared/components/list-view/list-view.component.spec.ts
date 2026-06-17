import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ListViewComponent } from './list-view.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

describe('ListViewComponent', () => {
  let component: ListViewComponent;
  let fixture: ComponentFixture<ListViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ListViewComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(ListViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with page 1', () => {
    expect(component.pageNumber()).toBe(1);
  });

  it('should initialize with lowest page size option', () => {
    expect(component.resultsPerPage()).toBe(10);
  });

  it('should have page size options', () => {
    expect(component.pageSizeOptions).toEqual([10, 25, 50]);
  });

  describe('onPageChange', () => {
    it('should update page number', () => {
      component.onPageChange(3);
      expect(component.pageNumber()).toBe(3);
    });
  });

  describe('onPageSizeChange', () => {
    it('should update results per page', () => {
      component.onPageSizeChange(25);
      expect(component.resultsPerPage()).toBe(25);
    });

    it('should reset page to 1', () => {
      component.onPageChange(5);
      component.onPageSizeChange(50);
      expect(component.pageNumber()).toBe(1);
    });
  });

  describe('totalVisiblePages', () => {
    it('should default to current page when no response', () => {
      expect(component.totalVisiblePages()).toBe(1);
    });
  });

  describe('hasMorePages', () => {
    it('should default to false when no response', () => {
      expect(component.hasMorePages()).toBe(false);
    });
  });

  describe('filter change resets page', () => {
    it('should reset pageNumber to 1 when category filter changes', () => {
      const filterState = TestBed.inject(FilterStateService);

      component.onPageChange(5);
      expect(component.pageNumber()).toBe(5);

      filterState.toggleCategory(1);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });

    it('should stay on page 1 if already on page 1 when filter changes', () => {
      const filterState = TestBed.inject(FilterStateService);

      expect(component.pageNumber()).toBe(1);

      filterState.toggleCategory(2);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });
  });
});
