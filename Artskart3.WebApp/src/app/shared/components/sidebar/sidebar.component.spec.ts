import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { SidebarComponent } from './sidebar.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { By } from '@angular/platform-browser';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { AuthService } from '../../services/auth/auth.service';
import { SavedFilterService } from '../../services/saved-filter/saved-filter.service';
import { SavedFilterDto } from '../../types/api.types';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';

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
      areas: [{ id: 1, fid: '03', name: 'Oslo', isCurrent: true }],
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

  it('renders red-list and alien-species categories with the shared badge', async () => {
    await flushAll();
    const badges = fixture.debugElement.queryAll(By.directive(RiskCategoryBadgeComponent));
    expect(badges.map((badge) => badge.injector.get(RiskCategoryBadgeComponent).code())).toEqual(['CR', 'SE']);
  });

  it('should set accordion heading from translation key', async () => {
    await flushAll();
    const accordionItems = fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item');
    const categoriesItem = Array.from(accordionItems).find(
      (el) => (el as Element).getAttribute('heading') === 'sidebar.categories',
    );
    expect(categoriesItem).toBeTruthy();
  });

  it('should render flat category sections with headings for each type', async () => {
    await flushAll();
    const accordionItems = fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item');
    const categoriesItem = Array.from(accordionItems).find(
      (el) => (el as Element).getAttribute('heading') === 'sidebar.categories',
    ) as Element;
    expect(categoriesItem).toBeTruthy();
    expect(categoriesItem.querySelector('adb-accordion')).toBeFalsy();
    const headings = categoriesItem.querySelectorAll('h6.category-section-heading');
    expect(headings.length).toBe(2);
    const flatLists = categoriesItem.querySelectorAll('.category-flat');
    expect(flatLists.length).toBe(2);
    const checkboxes = categoriesItem.querySelectorAll('.category-flat adb-checkbox');
    expect(checkboxes.length).toBe(2);
  });

  it('should show the search field', () => {
    const searchField = fixture.nativeElement.querySelector('.search-field');
    expect(searchField).toBeTruthy();
    const searchEl = searchField.querySelector('adb-search');
    expect(searchEl).toBeTruthy();
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

    it('should clear period inputs when the period filter is cleared externally', () => {
      component.periodFromInput.set('1900');
      component.periodToInput.set('2020');
      component.onApplyPeriod();

      filterState.clearPeriodYears();

      expect(component.periodFromInput()).toBe('');
      expect(component.periodToInput()).toBe('');
    });
  });

  describe('coordinate precision filtering', () => {
    it('should clear coordinate precision inputs when the filter is cleared externally', () => {
      component.coordinatePrecisionFromInput.set('10');
      component.coordinatePrecisionToInput.set('100');
      component.onApplyCoordinatePrecision();

      filterState.clearCoordinatePrecision();

      expect(component.coordinatePrecisionFromInput()).toBe('');
      expect(component.coordinatePrecisionToInput()).toBe('');
    });
  });

  describe('andre funnegenskaper', () => {
    // Project, dataset and catalog number, in template order.
    const otherPropertyFields = () =>
      fixture.debugElement
        .query(By.css('.other-properties-filter'))
        .queryAll(By.directive(AutocompleteComponent))
        .map((field) => field.componentInstance as AutocompleteComponent<unknown>);

    it('should keep every selected project, so several can be active at once', () => {
      component.onProjectSelected({ id: 14842, name: 'Kartlegging' });
      component.onProjectSelected({ id: 14843, name: 'Overvåking' });

      expect(filterState.selectedProjects()).toEqual([
        { id: 14842, name: 'Kartlegging' },
        { id: 14843, name: 'Overvåking' },
      ]);
    });

    it('should keep every selected dataset', () => {
      component.onDatasetSelected({ id: 26435, name: 'Aqua Kompetanse AS' });
      component.onDatasetSelected({ id: 26436, name: 'Artsobservasjoner' });

      expect(filterState.selectedDatasetIds()).toEqual([26435, 26436]);
    });

    it('should ignore a result without an id', () => {
      component.onProjectSelected({ name: 'Uten id' });
      component.onDatasetSelected({ name: 'Uten id' });

      expect(filterState.selectedProjects()).toEqual([]);
      expect(filterState.selectedDatasets()).toEqual([]);
    });

    it('should replace the catalog number when another one is selected', () => {
      component.onCatalogNumberSelected({ catalogNumber: 'NHM-123', observationIds: [101, 102] });
      component.onCatalogNumberSelected({ catalogNumber: 'NHM-456', observationIds: [201] });

      expect(filterState.catalogNumber()).toBe('NHM-456');
      expect(filterState.catalogObservationIds()).toEqual([201]);
    });

    it('should search each field through its own lookup endpoint', () => {
      component.searchProjects('kart').subscribe();
      component.searchDatasets('aqu').subscribe();
      component.searchCatalogNumbers('NHM').subscribe();

      httpTesting.expectOne((req) => req.url === '/api/Lookup/Projects' && req.params.get('search') === 'kart').flush([]);
      httpTesting.expectOne((req) => req.url === '/api/Lookup/Datasets' && req.params.get('search') === 'aqu').flush([]);
      httpTesting.expectOne((req) => req.url === '/api/Lookup/CatalogNumbers' && req.params.get('search') === 'NHM').flush([]);
    });

    // The label above already says what to type. An empty value keeps adb-search's own default placeholder away.
    it('should show no placeholder in the fields', () => {
      fixture.detectChanges();
      const searches = [...fixture.nativeElement.querySelectorAll('.other-properties-filter adb-search')] as HTMLElement[];

      expect(searches.map((search) => search.getAttribute('placeholder'))).toEqual(['', '', '']);
    });

    it('should show the observation count after project and dataset names', () => {
      const [projectField, datasetField] = otherPropertyFields();
      for (const field of [projectField, datasetField]) {
        field.results.set([{ id: 1, name: 'Kartlegging', observationCount: 1234 }]);
        field.showAutocomplete.set(true);
      }
      fixture.detectChanges();

      const options = [...fixture.nativeElement.querySelectorAll('.other-properties-filter .autocomplete-item')] as HTMLElement[];
      expect(options.map((option) => option.textContent?.trim())).toEqual(['Kartlegging (1,234)', 'Kartlegging (1,234)']);
    });

    it('should add to the filter when a result is picked in each field', () => {
      const [projectField, datasetField, catalogField] = otherPropertyFields();

      projectField.select({ id: 14842, name: 'Kartlegging' });
      datasetField.select({ id: 26435, name: 'Aqua Kompetanse AS' });
      catalogField.select({ catalogNumber: 'NHM-123', observationIds: [101] });

      expect(filterState.selectedProjectIds()).toEqual([14842]);
      expect(filterState.selectedDatasetIds()).toEqual([26435]);
      expect(filterState.catalogObservationIds()).toEqual([101]);
    });
  });
});

describe('SidebarComponent – lagrede filtre', () => {
  let fixture: ComponentFixture<SidebarComponent>;
  let filterState: FilterStateService;
  const isAuthenticated = signal(false);
  const defaultFilter = signal<SavedFilterDto | null>(null);
  const savedFilterService = { defaultFilter, activate: vi.fn() };

  beforeEach(async () => {
    isAuthenticated.set(false);
    defaultFilter.set(null);
    savedFilterService.activate.mockClear();

    await TestBed.configureTestingModule({
      imports: [SidebarComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated } },
        { provide: SavedFilterService, useValue: savedFilterService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SidebarComponent);
    filterState = TestBed.inject(FilterStateService);
    fixture.detectChanges();
  });

  const buttonTexts = (): string[] =>
    Array.from(fixture.nativeElement.querySelectorAll('adb-button') as NodeListOf<HTMLElement>).map((b) => b.textContent!.trim());

  it('viser ikke «Lagre filter» for anonyme brukere', () => {
    expect(buttonTexts()).not.toContain('savedFilters.saveButton');
  });

  it('viser «Lagre filter» for innloggede brukere og sier fra når den trykkes', () => {
    isAuthenticated.set(true);
    fixture.detectChanges();
    const emitted = vi.fn();
    fixture.componentInstance.saveFilterRequested.subscribe(emitted);

    const button = Array.from(fixture.nativeElement.querySelectorAll('adb-button') as NodeListOf<HTMLElement>).find(
      (b) => b.textContent!.trim() === 'savedFilters.saveButton',
    )!;
    button.click();

    expect(emitted).toHaveBeenCalled();
  });

  it('viser «Aktiver standardfilter» i stedet for «Tøm filter» når ingen filtre er satt', () => {
    isAuthenticated.set(true);
    defaultFilter.set({ id: 'a', name: 'Standard', filter: {}, isDefault: true });
    fixture.detectChanges();

    expect(buttonTexts()).toContain('savedFilters.useDefault');
    expect(buttonTexts()).not.toContain('sidebar.clearFilter');
  });

  it('viser «Tøm filter» når et filter er satt, selv med standardfilter', () => {
    isAuthenticated.set(true);
    defaultFilter.set({ id: 'a', name: 'Standard', filter: {}, isDefault: true });
    filterState.setImageFilter('withImage');
    fixture.detectChanges();

    expect(buttonTexts()).toContain('sidebar.clearFilter');
    expect(buttonTexts()).not.toContain('savedFilters.useDefault');
  });

  it('aktiverer standardfilteret', () => {
    isAuthenticated.set(true);
    const standard = { id: 'a', name: 'Standard', filter: { taxonIds: [1] }, isDefault: true };
    defaultFilter.set(standard);

    fixture.componentInstance.onUseDefaultFilter();

    expect(savedFilterService.activate).toHaveBeenCalledWith(standard);
  });
});
