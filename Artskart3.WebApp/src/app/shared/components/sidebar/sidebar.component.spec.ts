import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { SidebarComponent } from './sidebar.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

describe('SidebarComponent', () => {
  let component: SidebarComponent;
  let fixture: ComponentFixture<SidebarComponent>;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  const mockCategoryTypes = [
    { id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }] },
    { id: 2, name: 'Fremmedart', categories: [{ id: 7, code: 'SE', name: 'Svært høy risiko' }] },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(SidebarComponent);
    component = fixture.componentInstance;
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  async function flushCategories() {
    const req = httpTesting.expectOne('/api/Lookup/Categories');
    req.flush(mockCategoryTypes);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render accordion after categories load', async () => {
    await flushCategories();
    const accordion = fixture.nativeElement.querySelector('adb-accordion');
    expect(accordion).toBeTruthy();
  });

  it('should set accordion heading from translation key', async () => {
    await flushCategories();
    const accordionItem = fixture.nativeElement.querySelector('adb-accordion-item');
    expect(accordionItem.getAttribute('heading')).toBe('sidebar.categories');
  });

  it('should render nested accordions for category types', async () => {
    await flushCategories();
    const nestedAccordion = fixture.nativeElement.querySelector(
      'adb-accordion-item adb-accordion',
    );
    expect(nestedAccordion).toBeTruthy();
    const nestedItems = nestedAccordion.querySelectorAll('adb-accordion-item');
    expect(nestedItems.length).toBe(2);
    const checkboxes = nestedAccordion.querySelectorAll(
      'adb-accordion-item > adb-checkbox[slot="heading"]',
    );
    expect(checkboxes.length).toBe(2);
  });

  it('should hide the search field', () => {
    const searchField = fixture.nativeElement.querySelector('.search-field');
    expect(searchField).toBeTruthy();
    const styles = getComputedStyle(searchField);
    expect(styles.display).toBe('none');
  });

  describe('onCategoryToggle', () => {
    it('should toggle category in filter state', () => {
      component.onCategoryToggle(5);
      expect(filterState.selectedCategoryIds()).toEqual([5]);

      component.onCategoryToggle(5);
      expect(filterState.selectedCategoryIds()).toEqual([]);
    });
  });

  describe('isCategorySelected', () => {
    it('should return true for selected category', () => {
      filterState.toggleCategory(3);
      expect(component.isCategorySelected(3)).toBe(true);
    });

    it('should return false for unselected category', () => {
      expect(component.isCategorySelected(3)).toBe(false);
    });
  });
});
