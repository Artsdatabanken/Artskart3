import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { By } from '@angular/platform-browser';
import { ListViewComponent } from './list-view.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';

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

  it('renders the risk category badge with the category lookup code', async () => {
    const httpTesting = TestBed.inject(HttpTestingController);
    httpTesting.expectOne('/api/Lookup/Categories').flush([
      { id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }] },
    ]);
    httpTesting.expectOne('/api/Lookup/TaxonGroups').flush([]);
    httpTesting.expectOne('/api/Lookup/Areas').flush({});
    httpTesting.expectOne('/api/Search/Observation').flush({
      items: [{ id: 1, categoryId: 10 }],
      pageNumber: 1,
      resultsPerPage: 10,
      lookaheadCount: 0,
      hasMorePages: false,
    });

    await fixture.whenStable();
    fixture.detectChanges();

    const badge = fixture.debugElement.query(By.directive(RiskCategoryBadgeComponent));
    expect(badge).toBeTruthy();
    expect(badge.injector.get(RiskCategoryBadgeComponent).code()).toBe('CR');
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
