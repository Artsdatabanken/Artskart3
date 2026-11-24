import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
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
      areas: [
        { id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true },
        { id: 11, fid: '0302', name: 'Testkommune', isCurrent: true },
      ],
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

  async function flushAll(categoryTypes: unknown[] = mockCategoryTypes, areaResponse: object = mockAreaResponse) {
    httpTesting.expectOne('/api/Lookup/Categories').flush(categoryTypes);
    httpTesting.expectOne('/api/Lookup/Areas').flush(areaResponse);
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
    const checkboxes = categoriesItem.querySelectorAll('.category-flat adb-checkbox:not(.select-all)');
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

    it('orders the area section: custom area, protected area, cadastral unit, county and municipality', async () => {
      await flushAll();
      const headings = Array.from(fixture.nativeElement.querySelectorAll('h6.area-section-heading') as NodeListOf<Element>, (h) =>
        h.textContent?.trim(),
      );
      expect(headings.slice(0, 4)).toEqual([
        'sidebar.customArea.heading',
        'sidebar.restrictedArea',
        'sidebar.cadastralUnit',
        'sidebar.areaSearch',
      ]);
    });

    it('adds a picked protected area to the filter by its fid', () => {
      component.onRestrictedAreaSelected({ id: 400, fid: 'Naturbase VV00001234', name: 'Ytre Hvaler nasjonalpark', isCurrent: true });

      expect(filterState.selectedRestrictedAreaIds()).toEqual(['Naturbase VV00001234']);
    });

    describe('historical area limit', () => {
      const historical = (id: number) => ({
        area: { id, fid: String(id), name: `Område ${id}`, areaTypeId: 1, isCurrent: false, endYear: 2020 },
        detail: '',
      });

      beforeEach(() => {
        for (let id = 1; id <= component.maxHistoricalAreas; id++) component.onAreaSelected(historical(id));
      });

      it('refuses another historical area and says why', () => {
        component.onAreaSelected(historical(999));

        expect(filterState.selectedHistoricalAreaIds()).not.toContain(999);
        expect(component.historicalAreaLimitReached()).toBe(true);
      });

      it('hides the message again when an area is removed', () => {
        component.onAreaSelected(historical(999));
        filterState.removeHistoricalArea(1);

        expect(component.historicalAreaLimitReached()).toBe(false);
      });

      it('still adds current areas', () => {
        component.onAreaSelected({ area: { id: 301, fid: '0301', name: 'Oslo', areaTypeId: 1, isCurrent: true }, detail: '' });

        expect(filterState.selectedMunicipalityIds()).toContain('0301');
        expect(component.historicalAreaLimitReached()).toBe(false);
      });
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

    it('hides basis of record types without finds unless they are selected', async () => {
      await flushAll();
      const existing = mockBasisOfRecords.map(({ id, name, description, observationCount }) => ({ id, name, description, observationCount }));
      const withoutFinds = { id: 3, name: 'dna', description: 'DNA', observationCount: null };
      component.basisOfRecordsResource.set([...existing, withoutFinds, { ...withoutFinds, id: 4, observationCount: 0 }]);

      expect(component.basisOfRecords().map((b) => b.id)).toEqual([1, 2]);

      filterState.addBasisOfRecord(4);
      expect(component.basisOfRecords().map((b) => b.id)).toEqual([4, 1, 2]);
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

  // Clicking a "select all" that selects nothing would leave the checkbox looking checked with no filter set.
  it('shows no "select all" for a category type without categories', async () => {
    await flushAll([...mockCategoryTypes, { id: 3, name: 'Tom', categories: [] }]);
    const accordionItems = Array.from(fixture.nativeElement.querySelectorAll('adb-accordion-item')) as Element[];
    const categoriesItem = accordionItems.find((el) => el.getAttribute('heading') === 'sidebar.categories')!;
    const lists = Array.from(categoriesItem.querySelectorAll('.category-flat'));
    expect(lists.map((list) => list.querySelector('.select-all') !== null)).toEqual([true, true, false]);
  });

  describe('select all', () => {
    function accordionItem(heading: string): Element {
      const items = Array.from(fixture.nativeElement.querySelectorAll('adb-accordion-item')) as Element[];
      return items.find((el) => el.getAttribute('heading') === heading)!;
    }

    function selectAll(heading: string, index = 0): Element {
      return accordionItem(heading).querySelectorAll('adb-checkbox.select-all')[index];
    }

    function click(checkbox: Element): void {
      checkbox.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }

    beforeEach(async () => {
      await flushAll();
    });

    it('comes first in each list, with one per category type', () => {
      for (const heading of ['sidebar.taxonGroups', 'sidebar.behaviors', 'sidebar.basisOfRecords']) {
        const list = accordionItem(heading).querySelector('.checkbox-accordion-children')!;
        expect(list.firstElementChild!.classList).toContain('select-all');
      }
      const categoryLists = accordionItem('sidebar.categories').querySelectorAll('.category-flat');
      expect(Array.from(categoryLists).map((list) => list.firstElementChild!.classList.contains('select-all'))).toEqual([true, true]);
    });

    it('names the list it selects from for screen readers', () => {
      const checkbox = selectAll('sidebar.taxonGroups');
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('no', { sidebar: { selectAll: 'Velg alle', selectAllIn: 'Velg alle: {{list}}', taxonGroups: 'Artsgrupper' } });
      translate.use('no');
      fixture.detectChanges();

      expect(checkbox.getAttribute('aria-label')).toBe('Velg alle: Artsgrupper');
      expect(checkbox.textContent!.trim()).toBe('Velg alle');
    });

    it('selects every taxon group, then clears them on the next click', () => {
      click(selectAll('sidebar.taxonGroups'));
      expect(filterState.selectedTaxonGroupIds()).toEqual([1, 2]);

      click(selectAll('sidebar.taxonGroups'));
      expect(filterState.selectedTaxonGroupIds()).toEqual([]);
    });

    it('shows a partial selection, and completes it when clicked', () => {
      filterState.addTaxonGroup(2);
      fixture.detectChanges();
      const checkbox = selectAll('sidebar.taxonGroups');
      expect(checkbox.hasAttribute('indeterminate')).toBe(true);
      expect(checkbox.hasAttribute('checked')).toBe(false);

      click(checkbox);
      expect(filterState.selectedTaxonGroupIds()).toEqual([2, 1]);
      expect(checkbox.hasAttribute('checked')).toBe(true);
      expect(checkbox.hasAttribute('indeterminate')).toBe(false);
    });

    it('only selects and clears the categories of its own type', () => {
      filterState.addCategory(7);

      click(selectAll('sidebar.categories', 0));
      expect(filterState.selectedCategoryIds()).toEqual([7, 10]);

      click(selectAll('sidebar.categories', 0));
      expect(filterState.selectedCategoryIds()).toEqual([7]);
    });

    it('selects and clears every behavior', () => {
      click(selectAll('sidebar.behaviors'));
      expect(filterState.selectedBehaviorIds()).toEqual([1, 2]);

      click(selectAll('sidebar.behaviors'));
      expect(filterState.selectedBehaviorIds()).toEqual([]);
    });

    it('selects and clears every basis of record', () => {
      click(selectAll('sidebar.basisOfRecords'));
      expect(filterState.selectedBasisOfRecordIds()).toEqual([1, 2]);

      click(selectAll('sidebar.basisOfRecords'));
      expect(filterState.selectedBasisOfRecordIds()).toEqual([]);
    });
  });

  describe('registration status filtering', () => {
    const changeEvent = (value: string) => ({ target: { value } }) as unknown as Event;

    it('selects «Alle» when no registration status is set', () => {
      expect(component.registrationStatusValue()).toBe('all');
    });

    it('reflects the selected registration status', () => {
      filterState.setRegistrationStatus(2);
      expect(component.registrationStatusValue()).toBe('2');
    });

    it('sets the registration status from the selected radio', () => {
      component.onRegistrationStatusChange(changeEvent('3'));
      expect(filterState.selectedRegistrationStatusId()).toBe(3);
    });

    it('clears the registration status when «Alle» is selected', () => {
      filterState.setRegistrationStatus(1);
      component.onRegistrationStatusChange(changeEvent('all'));
      expect(filterState.selectedRegistrationStatusId()).toBeNull();
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

    it('should search catalog numbers from the same length as the API', () => {
      const [projectField, datasetField, catalogField] = otherPropertyFields();

      expect(catalogField.minLength()).toBe(2);
      expect(projectField.minLength()).toBe(1);
      expect(datasetField.minLength()).toBe(1);
    });

    it('should show the observation count after project and dataset names, and 0 when there is none', () => {
      const [projectField, datasetField] = otherPropertyFields();
      for (const field of [projectField, datasetField]) {
        field.results.set([
          { id: 1, name: 'Kartlegging', observationCount: 1234 },
          { id: 2, name: 'Tom', observationCount: null },
        ]);
        field.showAutocomplete.set(true);
      }
      fixture.detectChanges();

      const options = [...fixture.nativeElement.querySelectorAll('.other-properties-filter .autocomplete-item')] as HTMLElement[];
      expect(options.map((option) => option.textContent?.trim())).toEqual([
        'Kartlegging (1\u00a0234)',
        'Tom (0)',
        'Kartlegging (1\u00a0234)',
        'Tom (0)',
      ]);
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

  it('bryteren for underliggende taksa oppdaterer filteret', () => {
    component.onTaxonTreeToggle();
    fixture.detectChanges();

    const toggle = fixture.nativeElement.querySelectorAll('.taxon-tree-options adb-switch')[0];
    toggle.checked = false;
    toggle.dispatchEvent(new Event('change'));

    expect(filterState.includeDescendantTaxa()).toBe(false);
  });

  describe('active filter indicator', () => {
    const sectionItems = () =>
      Array.from(fixture.nativeElement.querySelectorAll(':scope > .sidebar-content > adb-accordion > adb-accordion-item')) as Element[];
    const sectionsWithIndicator = () =>
      sectionItems()
        .filter((item) => item.hasAttribute('indicator'))
        .map((item) => item.getAttribute('heading'));

    beforeEach(() => flushAll());

    it('shows no indicator with the default filter', () => {
      expect(sectionItems().length).toBe(10);
      expect(sectionsWithIndicator()).toEqual([]);
    });

    it('labels the indicator for screen readers', () => {
      expect(sectionItems().length).toBe(10);
      for (const item of sectionItems()) {
        expect(item.getAttribute('indicator-label')).toBe('sidebar.activeSelections');
      }
    });

    const cases: [string, string, (state: FilterStateService) => void][] = [
      ['sidebar.taxonGroups', 'a taxon group', (state) => state.toggleTaxonGroup(1)],
      ['sidebar.taxonTree', 'a taxon', (state) => state.addTaxon(42)],
      ['sidebar.categories', 'a category', (state) => state.toggleCategory(10)],
      ['sidebar.areas', 'a county', (state) => state.toggleCounty('03')],
      ['sidebar.areas', 'a municipality', (state) => state.toggleMunicipality('0301')],
      ['sidebar.areas', 'an ocean area', (state) => state.toggleOceanArea('5001')],
      ['sidebar.areas', 'a protected area', (state) => state.addRestrictedArea('VV00001234')],
      ['sidebar.areas', 'a cadastral unit', (state) => state.addCadastralUnit('1101-1/2')],
      ['sidebar.areas', 'a historical area', (state) => state.addHistoricalArea({ id: 99, name: 'Aker', isCurrent: false })],
      ['sidebar.areas', 'a drawn area', (state) => state.setCustomArea('POLYGON((0 0, 0 1, 1 1, 0 0))')],
      ['sidebar.coordinatePrecision', 'a lower precision only', (state) => state.setCoordinatePrecision(10, null)],
      ['sidebar.coordinatePrecision', 'an upper precision only', (state) => state.setCoordinatePrecision(null, 100)],
      ['sidebar.period', 'a start year only', (state) => state.setPeriod(2000, null)],
      ['sidebar.period', 'an end year only', (state) => state.setPeriod(null, 2010)],
      ['sidebar.period', 'a month', (state) => state.toggleMonth(5)],
      ['sidebar.behaviors', 'a behavior', (state) => state.toggleBehavior(1)],
      ['sidebar.basisOfRecords', 'a basis of record', (state) => state.toggleBasisOfRecord(1)],
      ['sidebar.basisOfRecords', 'a registration status', (state) => state.setRegistrationStatus(2)],
      ['sidebar.institutions', 'an institution', (state) => state.toggleInstitution(1)],
      ['sidebar.otherProperties', 'a project', (state) => state.addProject({ id: 1, name: 'Prosjekt' })],
      ['sidebar.otherProperties', 'a dataset', (state) => state.addDataset({ id: 2, name: 'Datasett' })],
      ['sidebar.otherProperties', 'a catalog number', (state) => state.setCatalogObservationIds([7])],
      ['sidebar.otherProperties', 'an image filter', (state) => state.setImageFilter('withImage')],
      ['sidebar.otherProperties', 'a sensitivity filter', (state) => state.setSensitiveFilter('sensitive')],
      ['sidebar.otherProperties', 'annotated observations', (state) => state.setAnnotationFilter('annotated')],
      ['sidebar.otherProperties', 'hidden observations included', (state) => state.setAnnotationFilter('all')],
    ];

    it.each(cases)('shows the indicator only on %s for %s', (heading, _description, select) => {
      select(filterState);
      fixture.detectChanges();
      expect(sectionsWithIndicator()).toEqual([heading]);
    });

    it('shows no indicator for filters without a control in the sidebar', () => {
      filterState.setIdentificationFilter('uncertain');
      filterState.setOriginFilter('introduced');
      fixture.detectChanges();
      expect(sectionsWithIndicator()).toEqual([]);
    });

    it('does not count settings that are not a filter on their own', () => {
      filterState.setIncludeDescendantTaxa(false);
      filterState.setAnnotationFilter('all');
      filterState.setAnnotationFilter('excludeHidden');
      fixture.detectChanges();
      expect(sectionsWithIndicator()).toEqual([]);
    });

    it('removes the indicators when the filter is cleared', () => {
      filterState.toggleTaxonGroup(1);
      filterState.setPeriod(2000, 2010);
      fixture.detectChanges();
      expect(sectionsWithIndicator()).toEqual(['sidebar.taxonGroups', 'sidebar.period']);

      component.onClearFilter();
      fixture.detectChanges();
      expect(sectionsWithIndicator()).toEqual([]);
    });
  });

  describe('vitenskapelige navn i artstreet', () => {
    const storageKey = 'artskart.taxonTree.showScientificNames';

    afterEach(() => localStorage.removeItem(storageKey));

    it('er av som standard', () => {
      expect(component.showScientificNames()).toBe(false);
    });

    it('henter lagret valg fra localStorage', () => {
      localStorage.setItem(storageKey, 'true');
      const restored = TestBed.createComponent(SidebarComponent).componentInstance;
      expect(restored.showScientificNames()).toBe(true);
    });

    it('oppdaterer og lagrer valget når bryteren endres', () => {
      component.onTaxonTreeToggle();
      fixture.detectChanges();

      const toggle = fixture.nativeElement.querySelectorAll('.taxon-tree-options adb-switch')[1];
      toggle.checked = true;
      toggle.dispatchEvent(new Event('change'));

      expect(component.showScientificNames()).toBe(true);
      expect(localStorage.getItem(storageKey)).toBe('true');
    });
  });

  describe('opening a filter from its chip', () => {
    type AccordionItem = HTMLElement & { open: boolean };
    // jsdom doesn't implement scrollIntoView.
    const originalScrollIntoView = Element.prototype.scrollIntoView;
    const scrollIntoView = vi.fn();

    beforeEach(() => {
      Element.prototype.scrollIntoView = scrollIntoView;
    });

    afterEach(() => {
      Element.prototype.scrollIntoView = originalScrollIntoView;
      scrollIntoView.mockClear();
    });

    function section(name: string): AccordionItem {
      return fixture.nativeElement.querySelector(`adb-accordion-item[data-filter-section="${name}"]`);
    }

    it('opens and scrolls to the section where the filter is set', async () => {
      await flushAll();
      await component.onOpenFilter('institutions');

      expect(section('institutions').open).toBe(true);
      expect(scrollIntoView.mock.contexts).toEqual([section('institutions')]);
    });

    it('unfolds the counties where only some municipalities are selected', async () => {
      await flushAll(mockCategoryTypes, {
        counties: {
          areas: [
            { id: 1, fid: '03', name: 'Oslo', isCurrent: true },
            { id: 2, fid: '11', name: 'Rogaland', isCurrent: true },
          ],
        },
        municipalities: {
          id: 2,
          name: 'Municipality',
          areas: [
            { id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true },
            { id: 11, fid: '1101', name: 'Eigersund', isCurrent: true },
            { id: 12, fid: '1103', name: 'Stavanger', isCurrent: true },
          ],
        },
      });
      filterState.addMunicipality('0301');
      filterState.addMunicipality('1101');
      fixture.detectChanges();

      await component.onOpenFilter('areas');

      const counties = section('areas').querySelectorAll('adb-accordion adb-accordion-item') as NodeListOf<AccordionItem>;
      expect([...counties].map((county) => county.open === true)).toEqual([false, true]);
    });

    it('starts the taxon tree and has it reveal the selected taxa', async () => {
      await component.onOpenFilter('taxonTree');
      await component.onOpenFilter('taxonTree');

      expect(component.taxonTreeOpened()).toBe(true);
      expect(component.taxonTreeReveal()).toBe(2);
    });

    it('reveals the selected taxa once their ancestries have been looked up', async () => {
      filterState.addTaxon(42);
      const opening = component.onOpenFilter('taxonTree');
      await new Promise((r) => setTimeout(r, 0));
      expect(component.taxonTreeOpened()).toBe(true);
      expect(component.taxonTreeReveal()).toBe(0);

      httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry').flush([{ id: 42, parentIds: [1] }]);
      await opening;
      expect(component.taxonTreeReveal()).toBe(1);
    });

    it("moves focus to the section's header, since the chip is scrolled out of view", async () => {
      await flushAll();
      // Stands in for the accordion item's own shadow DOM, which isn't defined in tests.
      const summary = document.createElement('summary');
      summary.tabIndex = 0;
      section('institutions').attachShadow({ mode: 'open' }).appendChild(summary);

      await component.onOpenFilter('institutions');

      expect(section('institutions').shadowRoot!.activeElement).toBe(summary);
    });

    it('opens the section of a chip that is clicked', async () => {
      await flushAll();
      filterState.addInstitution(1);
      fixture.detectChanges();

      fixture.nativeElement.querySelector('app-filter-chips .filter-chip-open').click();

      expect(section('institutions').open).toBe(true);
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
