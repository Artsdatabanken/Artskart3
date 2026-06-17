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

  const mockAreaResponse = {
    counties: {
      fastlandsNorge: [{ id: 1, fid: '03', name: 'Oslo', isCurrent: true }],
    },
    municipalities: {
      id: 2,
      name: 'Municipality',
      areas: [{ id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true }],
    },
  };

  const mockInstitutions = [
    { id: 1, name: 'NINA', code: 'NINA', observationCount: 100 },
    { id: 2, name: 'NIBIO', code: 'NIBIO', observationCount: 50 },
  ];

  const mockBehaviors = [
    { id: 1, name: 'Terrestrisk', variants: null, observationCount: 200 },
    { id: 2, name: 'Akvatisk', variants: null, observationCount: 150 },
  ];

  const mockBasisOfRecords = [
    { id: 1, name: 'humanobservation', description: 'Human Observation', variants: null, observationCount: 500 },
    { id: 2, name: 'machine_observation', description: 'Machine Observation', variants: null, observationCount: 300 },
  ];

  const mockTaxonGroups = [
    { id: 1, name: 'Fugler', observationCount: 1000 },
    { id: 2, name: 'Pattedyr', observationCount: 800 },
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
    httpTesting.expectOne('/api/Lookup/Areas').flush(mockAreaResponse);
    httpTesting.expectOne('/api/Lookup/Institutions').flush(mockInstitutions);
    httpTesting.expectOne('/api/Lookup/Behaviors').flush(mockBehaviors);
    httpTesting.expectOne('/api/Lookup/BasisOfRecords').flush(mockBasisOfRecords);
    httpTesting.expectOne('/api/Lookup/TaxonGroups').flush(mockTaxonGroups);
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
      (el) => (el as Element).getAttribute('heading') === 'sidebar.categories',
    );
    expect(categoriesItem).toBeTruthy();
  });

  it('should render nested accordions for category types', async () => {
    await flushAll();
    const accordionItems = fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item');
    const categoriesItem = Array.from(accordionItems).find(
      (el) => (el as Element).getAttribute('heading') === 'sidebar.categories',
    ) as Element;
    expect(categoriesItem).toBeTruthy();
    const nestedAccordion = categoriesItem.querySelector('adb-accordion')!;
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

    it('should toggle municipality in filter state', () => {
      component.onMunicipalityToggle('0301');
      expect(filterState.selectedMunicipalityIds()).toEqual(['0301']);

      component.onMunicipalityToggle('0301');
      expect(filterState.selectedMunicipalityIds()).toEqual([]);
    });

    it('should select all municipalities when county is toggled', async () => {
      await flushAll();
      const groups = component.countyGroups();
      expect(groups.length).toBe(1);

      component.onCountyToggle(groups[0]);
      expect(filterState.selectedMunicipalityIds()).toContain('0301');
    });

    it('should deselect all municipalities when county is toggled again', async () => {
      await flushAll();
      const groups = component.countyGroups();
      component.onCountyToggle(groups[0]);
      component.onCountyToggle(groups[0]);
      expect(filterState.selectedMunicipalityIds()).toEqual([]);
    });

    it('should report isMunicipalitySelected correctly', () => {
      filterState.addMunicipality('0301');
      expect(component.isMunicipalitySelected('0301')).toBe(true);
      expect(component.isMunicipalitySelected('0602')).toBe(false);
    });
  });

  describe('institution filtering', () => {
    it('should render institutions accordion after load', async () => {
      await flushAll();
      const accordionItems = fixture.nativeElement.querySelectorAll('adb-accordion-item');
      const institutionItem = Array.from(accordionItems).find(
        (el) => (el as Element).getAttribute('heading') === 'sidebar.institutions',
      );
      expect(institutionItem).toBeTruthy();
    });

    it('should toggle institution in filter state', () => {
      component.onInstitutionToggle(1);
      expect(filterState.selectedInstitutionIds()).toEqual([1]);

      component.onInstitutionToggle(1);
      expect(filterState.selectedInstitutionIds()).toEqual([]);
    });

    it('should report isInstitutionSelected correctly', () => {
      filterState.addInstitution(2);
      expect(component.isInstitutionSelected(2)).toBe(true);
      expect(component.isInstitutionSelected(99)).toBe(false);
    });
  });

  describe('behavior filtering', () => {
    it('should render behaviors accordion after load', async () => {
      await flushAll();
      const accordionItems = fixture.nativeElement.querySelectorAll('adb-accordion-item');
      const behaviorItem = Array.from(accordionItems).find(
        (el) => (el as Element).getAttribute('heading') === 'sidebar.behaviors',
      );
      expect(behaviorItem).toBeTruthy();
    });

    it('should toggle behavior in filter state', () => {
      component.onBehaviorToggle(1);
      expect(filterState.selectedBehaviorIds()).toEqual([1]);

      component.onBehaviorToggle(1);
      expect(filterState.selectedBehaviorIds()).toEqual([]);
    });

    it('should report isBehaviorSelected correctly', () => {
      filterState.addBehavior(2);
      expect(component.isBehaviorSelected(2)).toBe(true);
      expect(component.isBehaviorSelected(99)).toBe(false);
    });
  });

  describe('basis of record filtering', () => {
    it('should render basisOfRecords accordion after load', async () => {
      await flushAll();
      const accordionItems = fixture.nativeElement.querySelectorAll('adb-accordion-item');
      const basisOfRecordItem = Array.from(accordionItems).find(
        (el) => (el as Element).getAttribute('heading') === 'sidebar.basisOfRecords',
      );
      expect(basisOfRecordItem).toBeTruthy();
    });

    it('should toggle basisOfRecord in filter state', () => {
      component.onBasisOfRecordToggle(1);
      expect(filterState.selectedBasisOfRecordIds()).toEqual([1]);

      component.onBasisOfRecordToggle(1);
      expect(filterState.selectedBasisOfRecordIds()).toEqual([]);
    });

    it('should report isBasisOfRecordSelected correctly', () => {
      filterState.addBasisOfRecord(2);
      expect(component.isBasisOfRecordSelected(2)).toBe(true);
      expect(component.isBasisOfRecordSelected(99)).toBe(false);
    });
  });

  describe('period filtering', () => {
    it('should render period accordion', async () => {
      await flushAll();
      const accordionItems = fixture.nativeElement.querySelectorAll('adb-accordion-item');
      const periodItem = Array.from(accordionItems).find(
        (el) => (el as Element).getAttribute('heading') === 'sidebar.period',
      );
      expect(periodItem).toBeTruthy();
    });

    it('should filter non-numeric characters from period from input', () => {
      const event = { target: { value: '19abc00' } } as unknown as Event;
      component.onPeriodFromChange(event);
      expect(component.periodFromInput()).toBe('1900');
      expect((event.target as HTMLInputElement).value).toBe('1900');
    });

    it('should filter non-numeric characters from period to input', () => {
      const event = { target: { value: '20x26' } } as unknown as Event;
      component.onPeriodToChange(event);
      expect(component.periodToInput()).toBe('2026');
      expect((event.target as HTMLInputElement).value).toBe('2026');
    });

    it('should limit period input to 4 characters', () => {
      const event = { target: { value: '19001' } } as unknown as Event;
      component.onPeriodFromChange(event);
      expect(component.periodFromInput()).toBe('1900');
    });

    it('should apply period to filter state', () => {
      component.periodFromInput.set('1900');
      component.periodToInput.set('2020');
      component.onApplyPeriod();
      expect(filterState.periodFrom()).toBe(1900);
      expect(filterState.periodTo()).toBe(2020);
    });

    it('should swap values when from > to', () => {
      component.periodFromInput.set('2020');
      component.periodToInput.set('1900');
      component.onApplyPeriod();
      expect(filterState.periodFrom()).toBe(1900);
      expect(filterState.periodTo()).toBe(2020);
      expect(component.periodFromInput()).toBe('1900');
      expect(component.periodToInput()).toBe('2020');
    });

    it('should allow empty values (null)', () => {
      component.periodFromInput.set('');
      component.periodToInput.set('2020');
      component.onApplyPeriod();
      expect(filterState.periodFrom()).toBeNull();
      expect(filterState.periodTo()).toBe(2020);
    });
  });
});
