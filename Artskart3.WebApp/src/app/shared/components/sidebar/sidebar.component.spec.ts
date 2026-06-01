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

  const mockAreaTypes = [
    {
      id: 1,
      name: 'County',
      areas: [{ id: 1, fid: '03', name: 'Oslo', isCurrent: true }],
    },
    {
      id: 2,
      name: 'Municipality',
      areas: [{ id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true }],
    },
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

  async function flushAll() {
    httpTesting.expectOne('/api/Lookup/Categories').flush(mockCategoryTypes);
    httpTesting.expectOne('/api/Lookup/Areas').flush(mockAreaTypes);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render accordion after categories load', async () => {
    await flushAll();
    const accordion = fixture.nativeElement.querySelector('adb-accordion');
    expect(accordion).toBeTruthy();
  });

  it('should set accordion heading from translation key', async () => {
    await flushAll();
    const accordionItems = fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item');
    const categoriesItem = Array.from(accordionItems).find(
      (el: any) => el.getAttribute('heading') === 'sidebar.categories',
    );
    expect(categoriesItem).toBeTruthy();
  });

  it('should render nested accordions for category types', async () => {
    await flushAll();
    const accordionItems = fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item');
    const categoriesItem: any = Array.from(accordionItems).find(
      (el: any) => el.getAttribute('heading') === 'sidebar.categories',
    );
    expect(categoriesItem).toBeTruthy();
    const nestedAccordion = categoriesItem.querySelector('adb-accordion');
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

  describe('area filtering', () => {
    it('should render areas accordion after load', async () => {
      await flushAll();
      const accordions = fixture.nativeElement.querySelectorAll('adb-accordion');
      // Categories accordion + Areas accordion
      expect(accordions.length).toBeGreaterThanOrEqual(2);
    });

    it('should toggle area in filter state', () => {
      component.onAreaToggle('0301');
      expect(filterState.selectedAreaIds()).toEqual(['0301']);

      component.onAreaToggle('0301');
      expect(filterState.selectedAreaIds()).toEqual([]);
    });

    it('should select all municipalities and county when county is toggled', async () => {
      await flushAll();
      const groups = component.countyGroups();
      expect(groups.length).toBe(1);

      component.onCountyToggle(groups[0]);
      expect(filterState.selectedAreaIds()).toContain('03');
      expect(filterState.selectedAreaIds()).toContain('0301');
    });

    it('should deselect all when county is toggled again', async () => {
      await flushAll();
      const groups = component.countyGroups();
      component.onCountyToggle(groups[0]);
      component.onCountyToggle(groups[0]);
      expect(filterState.selectedAreaIds()).toEqual([]);
    });

    it('should report isAreaSelected correctly', () => {
      filterState.addArea('0301');
      expect(component.isAreaSelected('0301')).toBe(true);
      expect(component.isAreaSelected('03')).toBe(false);
    });
  });
});
