import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { By } from '@angular/platform-browser';
import { FilterChipsComponent } from './filter-chips.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { TooltipComponent } from '../tooltip/tooltip.component';

describe('FilterChipsComponent', () => {
  let component: FilterChipsComponent;
  let fixture: ComponentFixture<FilterChipsComponent>;
  let filterState: FilterStateService;
  let translate: TranslateService;
  let httpTesting: HttpTestingController;

  // Finnmark-lignende oppsett: ett fylke med 18 kommuner.
  const mockAreaResponse = {
    counties: {
      areas: [{ id: 1, fid: '56', name: 'Finnmark', isCurrent: true }],
    },
    municipalities: {
      id: 2,
      name: 'Municipality',
      areas: Array.from({ length: 18 }, (_, i) => ({
        id: 100 + i,
        fid: `56${String(i + 1).padStart(2, '0')}`,
        name: `Kommune ${i + 1}`,
        isCurrent: true,
      })),
    },
    svalbardBjørnøyaAndJanMayen: {
      id: 3,
      name: 'Svalbard',
      areas: [{ id: 200, fid: '21', name: 'Svalbard', isCurrent: true }],
    },
    oceanAreas: {
      id: 4,
      name: 'Havområder',
      areas: [{ id: 300, fid: 'ocean1', name: 'Norskehavet', isCurrent: true }],
    },
    restrictedAreas: {
      id: 3,
      name: 'Verneområder',
      areas: [{ id: 400, fid: 'Naturbase VV00001234', name: 'Ytre Hvaler nasjonalpark', isCurrent: true }],
    },
  };

  const mockTaxonGroups = [
    { id: 1, name: 'Fugler' },
    { id: 2, name: 'Pattedyr' },
  ];
  const mockBehaviors = [{ id: 1, name: 'Terrestrisk', description: 'På land' }];
  const mockBasisOfRecords = [{ id: 1, name: 'humanobservation', description: 'Human Observation' }];
  const mockInstitutions = [{ id: 5, name: 'NINA' }];

  const mockCategoryTypes = [
    { id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }, { id: 11, code: 'EN', name: 'Sterkt truet' }] },
    { id: 2, name: 'Fremmedart', categories: [{ id: 20, code: 'SE', name: 'Svært høy risiko' }] },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FilterChipsComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(FilterChipsComponent);
    component = fixture.componentInstance;
    filterState = TestBed.inject(FilterStateService);
    translate = TestBed.inject(TranslateService);
    httpTesting = TestBed.inject(HttpTestingController);
    translate.setDefaultLang('no');
    translate.use('no');
    fixture.detectChanges();
  });

  afterEach(() => httpTesting.verify());

  function flushAreas() {
    httpTesting.expectOne('/api/Lookup/Areas').flush(mockAreaResponse);
  }

  function flushCategories() {
    httpTesting.expectOne('/api/Lookup/Categories').flush(mockCategoryTypes);
  }

  function flushLookups() {
    flushAreas();
    flushCategories();
    flushListLookups();
  }

  function flushListLookups() {
    httpTesting.expectOne('/api/Lookup/TaxonGroups').flush(mockTaxonGroups);
    httpTesting.expectOne('/api/Lookup/Behaviors').flush(mockBehaviors);
    httpTesting.expectOne('/api/Lookup/BasisOfRecords').flush(mockBasisOfRecords);
    httpTesting.expectOne('/api/Lookup/Institutions').flush(mockInstitutions);
  }

  function details(chipId: string): string | undefined {
    return component.chips().find((c) => c.id === chipId)?.details;
  }

  function selectAllFinnmarkMunicipalities() {
    for (let i = 1; i <= 18; i++) {
      filterState.addMunicipality(`56${String(i).padStart(2, '0')}`);
    }
  }

  it('should create', () => {
    expect(component).toBeTruthy();
    flushLookups();
  });

  it('should show no chips when no filters are active', () => {
    flushLookups();
    expect(component.chips().length).toBe(0);
  });

  it('should show a count chip when taxon groups are selected', () => {
    flushLookups();
    filterState.addTaxonGroup(1);
    filterState.addTaxonGroup(2);
    const chip = component.chips().find((c) => c.id === 'taxonGroups');
    expect(chip).toBeTruthy();
    expect(chip!.suffix).toBe('(2)');
    expect(chip!.label).toContain('(2)');
  });

  describe('kategorichips', () => {
    it('viser én chip per kategoritype (Rødlista, Fremmedartslista)', () => {
      flushLookups();
      filterState.addCategory(10);
      filterState.addCategory(11);
      filterState.addCategory(20);
      const chips = component.chips();
      expect(chips.find((c) => c.id === 'categories:Rødliste')!.suffix).toBe('(2)');
      expect(chips.find((c) => c.id === 'categories:Fremmedart')!.suffix).toBe('(1)');
      expect(chips.find((c) => c.id === 'categories:other')).toBeUndefined();
    });

    it('fjerner kun sin egen kategoritype når chip lukkes', () => {
      flushLookups();
      filterState.addCategory(10);
      filterState.addCategory(20);
      component.chips().find((c) => c.id === 'categories:Rødliste')!.clear();
      expect(filterState.selectedCategoryIds()).toEqual([20]);
    });

    it('samler kategorier av ukjent type i en felles-chip', () => {
      flushLookups();
      filterState.addCategory(10);
      filterState.addCategory(999);
      const chip = component.chips().find((c) => c.id === 'categories:other');
      expect(chip!.suffix).toBe('(1)');
      chip!.clear();
      expect(filterState.selectedCategoryIds()).toEqual([10]);
    });
  });

  describe('områdechips', () => {
    it('teller et fullt valgt fylke som 1 (implisitte valg telles ikke)', () => {
      flushLookups();
      selectAllFinnmarkMunicipalities();
      const chip = component.chips().find((c) => c.id === 'areas:mainland');
      expect(chip!.suffix).toBe('(1)');
    });

    it('teller valgte kommuner når ett implisitt valg fjernes', () => {
      flushLookups();
      selectAllFinnmarkMunicipalities();
      filterState.removeMunicipality('5601');
      const chip = component.chips().find((c) => c.id === 'areas:mainland');
      expect(chip!.suffix).toBe('(17)');
    });

    it('viser egne chips for Svalbard og havområder', () => {
      flushLookups();
      filterState.addCounty('21');
      filterState.toggleOceanArea('ocean1');
      const chips = component.chips();
      expect(chips.find((c) => c.id === 'areas:svalbard')!.suffix).toBe('(1)');
      expect(chips.find((c) => c.id === 'areas:ocean')!.suffix).toBe('(1)');
      expect(chips.find((c) => c.id === 'areas:mainland')).toBeUndefined();
    });

    it('fjerner kun egne seksjoner når chip lukkes', () => {
      flushLookups();
      selectAllFinnmarkMunicipalities();
      filterState.addCounty('21');
      component.chips().find((c) => c.id === 'areas:mainland')!.clear();
      expect(filterState.selectedMunicipalityIds().length).toBe(0);
      expect(filterState.selectedCountyIds()).toEqual(['21']);
    });

    it('teller alt som fastland og viser ingen Svalbard-chip om områdene ikke lastes', () => {
      // Feilet request: catchError gjør tilstanden permanent, så chips må likevel
      // telle og kunne fjerne valgene.
      httpTesting.expectOne('/api/Lookup/Areas').error(new ProgressEvent('error'));
      flushCategories();
      flushListLookups();
      filterState.addCounty('21');
      filterState.addMunicipality('5601');
      const chips = component.chips();
      expect(chips.find((c) => c.id === 'areas:mainland')!.suffix).toBe('(2)');
      expect(chips.find((c) => c.id === 'areas:svalbard')).toBeUndefined();
    });

    it('lar fastlands-chippen fjerne alle områdevalg når områdene ikke lastes', () => {
      httpTesting.expectOne('/api/Lookup/Areas').error(new ProgressEvent('error'));
      flushCategories();
      flushListLookups();
      filterState.addCounty('21');
      filterState.addMunicipality('5601');
      component.chips().find((c) => c.id === 'areas:mainland')!.clear();
      expect(filterState.selectedCountyIds().length).toBe(0);
      expect(filterState.selectedMunicipalityIds().length).toBe(0);
    });
  });

  describe('søk-og-velg-chips', () => {
    it('viser én felles chip for arter fra artsøk og takson-tre', () => {
      flushLookups();
      // Artsøk (addTaxon) og takson-tre (setTaxons) skriver til samme utvalg
      filterState.addTaxon(1234);
      filterState.setTaxons([1234, 5678]);
      const chip = component.chips().find((c) => c.id === 'taxons');
      expect(chip!.suffix).toBe('(2)');
      chip!.clear();
      expect(filterState.selectedTaxonIds().length).toBe(0);
    });

    it('viser én chip per prosjekt, og lukking fjerner bare det prosjektet', () => {
      flushLookups();
      filterState.addProject({ id: 42, name: 'Artsprosjektet' });
      filterState.addProject({ id: 43, name: 'Kartlegging' });
      const projectChips = component.chips().filter((c) => c.id.startsWith('project:'));
      expect(projectChips.map((c) => c.text)).toEqual(['sidebar.project: Artsprosjektet', 'sidebar.project: Kartlegging']);
      projectChips[0].clear();
      expect(filterState.selectedProjects()).toEqual([{ id: 43, name: 'Kartlegging' }]);
    });

    it('viser én chip per datasett, og lukking fjerner bare det datasettet', () => {
      flushLookups();
      filterState.addDataset({ id: 7, name: 'Datasett X' });
      filterState.addDataset({ id: 8, name: 'Datasett Y' });
      const datasetChips = component.chips().filter((c) => c.id.startsWith('dataset:'));
      expect(datasetChips.map((c) => c.text)).toEqual(['sidebar.dataset: Datasett X', 'sidebar.dataset: Datasett Y']);
      datasetChips[1].clear();
      expect(filterState.selectedDatasets()).toEqual([{ id: 7, name: 'Datasett X' }]);
    });

    it('viser ID-en før navnet er slått opp, så filteret aldri er usynlig', () => {
      flushLookups();
      filterState.addProject({ id: 42, name: '' });
      expect(component.chips().find((c) => c.id === 'project:42')!.text).toBe('sidebar.project: #42');
    });

    it('viser én chip per matrikkelenhet, og lukking fjerner bare den', () => {
      flushLookups();
      filterState.addCadastralUnit('1101-1/2');
      filterState.addCadastralUnit('0301-207/1');
      const cadastralUnitChips = component.chips().filter((c) => c.id.startsWith('cadastralUnit:'));
      expect(cadastralUnitChips.map((c) => c.text)).toEqual(['sidebar.cadastralUnit: 1101-1/2', 'sidebar.cadastralUnit: 0301-207/1']);
      cadastralUnitChips[0].clear();
      expect(filterState.selectedCadastralUnitIds()).toEqual(['0301-207/1']);
    });

    it('shows a chip per protected area by name, and closing it removes only that one', () => {
      flushLookups();
      filterState.addRestrictedArea('Naturbase VV00001234');
      filterState.addRestrictedArea('Naturbase VV00005678');
      const chips = component.chips().filter((c) => c.id.startsWith('restrictedArea:'));
      // The second one isn't in the area list, so it keeps its fid.
      expect(chips.map((c) => c.text)).toEqual([
        'sidebar.restrictedArea: Ytre Hvaler nasjonalpark',
        'sidebar.restrictedArea: Naturbase VV00005678',
      ]);
      chips[0].clear();
      expect(filterState.selectedRestrictedAreaIds()).toEqual(['Naturbase VV00005678']);
    });

    it('shows the fid of a protected area until the area list has loaded', () => {
      filterState.addRestrictedArea('Naturbase VV00001234');
      const chip = () => component.chips().find((c) => c.id === 'restrictedArea:Naturbase VV00001234')!;
      expect(chip().text).toBe('sidebar.restrictedArea: Naturbase VV00001234');

      flushLookups();
      expect(chip().text).toBe('sidebar.restrictedArea: Ytre Hvaler nasjonalpark');
    });

    it('shows a chip per historical area with its kind', () => {
      flushLookups();
      filterState.addHistoricalArea({ id: 2, fid: '1902', name: 'Tromsø', areaTypeId: 1, isCurrent: false, countyName: 'Troms', endYear: 2020 });
      filterState.addHistoricalArea({ id: 669, fid: '54', name: 'Troms og Finnmark', areaTypeId: 2, isCurrent: false, endYear: 2024 });
      const chips = component.chips().filter((c) => c.id.startsWith('historicalArea:'));
      expect(chips.map((c) => c.text)).toEqual([
        'sidebar.areaSearch: Tromsø (sidebar.areaSearchType.historicalMunicipality)',
        'sidebar.areaSearch: Troms og Finnmark (sidebar.areaSearchType.historicalCounty)',
      ]);
      chips[0].clear();
      expect(filterState.selectedHistoricalAreaIds()).toEqual([669]);
    });

    it('shows the id of a historical area that has not been looked up yet', () => {
      flushLookups();
      filterState.selectedHistoricalAreas.set([{ id: 669, isCurrent: false }]);
      expect(component.chips().find((c) => c.id === 'historicalArea:669')!.text).toBe('sidebar.areaSearch: #669');
    });

    it('viser chip for egendefinert område, og lukking fjerner det', () => {
      flushLookups();
      filterState.setCustomArea('POLYGON((0 0,10 0,10 10,0 0))');
      const chip = component.chips().find((c) => c.id === 'customArea')!;
      expect(chip.text).toBe('sidebar.customArea.heading');
      chip.clear();
      expect(filterState.customAreaWkt()).toBeNull();
    });

    it('viser chip for katalognummer', () => {
      flushLookups();
      filterState.setCatalogNumber('ABC-123');
      filterState.setCatalogObservationIds([1, 2]);
      expect(component.chips().find((c) => c.id === 'catalogNumber')!.text).toContain('ABC-123');
    });
  });

  describe('radiochips', () => {
    it('viser ingen chip for standardvalg', () => {
      flushLookups();
      filterState.setRegistrationStatus(null);
      filterState.setImageFilter('all');
      filterState.setAnnotationFilter('excludeHidden');
      filterState.setSensitiveFilter('all');
      expect(component.chips().find((c) => c.id === 'registrationStatus')).toBeUndefined();
      expect(component.chips().find((c) => c.id === 'imageFilter')).toBeUndefined();
      expect(component.chips().find((c) => c.id === 'annotationFilter')).toBeUndefined();
      expect(component.chips().find((c) => c.id === 'sensitiveFilter')).toBeUndefined();
    });

    it('viser chip med filtrets tittel og valgt radioknapp', () => {
      flushLookups();
      filterState.setRegistrationStatus(2);
      filterState.setImageFilter('withoutImage');
      filterState.setAnnotationFilter('hidden');
      filterState.setSensitiveFilter('sensitive');
      const chips = component.chips();
      // Uten lastede oversettelser returnerer instant() nøklene
      expect(chips.find((c) => c.id === 'registrationStatus')!.text).toContain('sidebar.registreringStatus.absent');
      expect(chips.find((c) => c.id === 'imageFilter')!.text).toContain('sidebar.imageWithout');
      expect(chips.find((c) => c.id === 'annotationFilter')!.text).toContain('sidebar.annotationHidden');
      expect(chips.find((c) => c.id === 'sensitiveFilter')!.text).toBe('sidebar.sensitivity: sidebar.sensitiveOnly');
    });

    it('tilbakestiller til standardvalg når chip lukkes', () => {
      flushLookups();
      filterState.setRegistrationStatus(2);
      filterState.setImageFilter('withoutImage');
      filterState.setAnnotationFilter('annotated');
      filterState.setSensitiveFilter('notSensitive');
      const chips = component.chips();
      chips.find((c) => c.id === 'registrationStatus')!.clear();
      chips.find((c) => c.id === 'imageFilter')!.clear();
      chips.find((c) => c.id === 'annotationFilter')!.clear();
      chips.find((c) => c.id === 'sensitiveFilter')!.clear();
      expect(filterState.selectedRegistrationStatusId()).toBeNull();
      expect(filterState.imageFilter()).toBe('all');
      expect(filterState.annotationFilter()).toBe('excludeHidden');
      expect(filterState.sensitiveFilter()).toBe('all');
    });

    it.each([
      ['all', 'sidebar.annotationAll'],
      ['hidden', 'sidebar.annotationHidden'],
      ['annotated', 'sidebar.annotationAnnotated'],
    ] as const)('viser riktig tekst for annoteringsvalget %s', (option, expected) => {
      flushLookups();
      filterState.setAnnotationFilter(option);
      expect(component.chips().find((c) => c.id === 'annotationFilter')!.text).toContain(expected);
    });
  });

  describe('underliggende taksa', () => {
    it('viser chip bare når den er av og det er valgt taksa', () => {
      flushLookups();
      filterState.setIncludeDescendantTaxa(false);
      expect(component.chips().find((c) => c.id === 'includeDescendantTaxa')).toBeUndefined();

      filterState.setTaxons([1]);
      expect(component.chips().find((c) => c.id === 'includeDescendantTaxa')!.text).toBe('sidebar.excludesDescendantTaxa');
    });

    it('slår underliggende taksa på igjen når chip lukkes', () => {
      flushLookups();
      filterState.setTaxons([1]);
      filterState.setIncludeDescendantTaxa(false);
      component.chips().find((c) => c.id === 'includeDescendantTaxa')!.clear();
      expect(filterState.includeDescendantTaxa()).toBe(true);
      expect(filterState.selectedTaxonIds()).toEqual([1]);
    });
  });

  describe('til/fra-chips', () => {
    it('viser chip for koordinatpresisjon og fjerner den', () => {
      flushLookups();
      filterState.setCoordinatePrecision(0, 500);
      const chip = component.chips().find((c) => c.id === 'coordinatePrecision');
      expect(chip).toBeTruthy();
      chip!.clear();
      expect(filterState.coordinatePrecisionFrom()).toBeNull();
      expect(filterState.coordinatePrecisionTo()).toBeNull();
    });

    it('viser chip for periode og fjerner kun årstallene', () => {
      flushLookups();
      filterState.setPeriod(1990, 2020);
      filterState.toggleMonth(6);
      const chip = component.chips().find((c) => c.id === 'period');
      expect(chip).toBeTruthy();
      chip!.clear();
      expect(filterState.periodFrom()).toBeNull();
      expect(filterState.periodTo()).toBeNull();
      // Måneder er et eget filter med egen chip og røres ikke
      expect(filterState.selectedMonths()).toEqual([6]);
    });

    it('viser egen chip for måneder som fjerner kun månedene', () => {
      flushLookups();
      filterState.setPeriod(1990, 2020);
      filterState.toggleMonth(3);
      filterState.toggleMonth(6);
      const chip = component.chips().find((c) => c.id === 'months');
      expect(chip!.suffix).toBe('(2)');
      chip!.clear();
      expect(filterState.selectedMonths().length).toBe(0);
      expect(filterState.periodFrom()).toBe(1990);
    });
  });

  describe('trunkering og tilgjengelighet', () => {
    it('describes the chip with its full text in a tooltip', () => {
      flushLookups();
      filterState.addProject({ id: 42, name: 'Et prosjekt med et veldig langt navn som ikke får plass' });
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      const button = el.querySelector('.filter-chip-open')!;
      const tooltip = el.querySelector(`#${button.getAttribute('aria-describedby')}`);
      expect(tooltip!.textContent).toContain('Et prosjekt med et veldig langt navn som ikke får plass');
    });

    it('holder suffix i eget element slik at det ikke trunkeres', () => {
      flushLookups();
      filterState.addTaxonGroup(1);
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      const suffix = el.querySelector('.filter-chip-suffix');
      expect(suffix!.textContent).toContain('(1)');
    });

    it('gir lukkeknappen et oversatt navn med chipens tekst', () => {
      flushLookups();
      translate.setTranslation('no', { sidebar: { removeFilterAriaLabel: 'Fjern filter {{filter}}' } }, true);
      filterState.addProject({ id: 42, name: 'Et prosjekt' });
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      const removeButton = el.querySelector('.filter-chip adb-icon-button');
      expect(removeButton!.getAttribute('aria-label')).toBe(`Fjern filter ${component.chips()[0].label}`);
    });
  });

  describe('tooltip with the selections', () => {
    it('lists the selected items by name in list order', () => {
      flushLookups();
      filterState.addTaxonGroup(2);
      filterState.addTaxonGroup(1);
      filterState.addInstitution(5);
      expect(details('taxonGroups')).toBe('Fugler, Pattedyr');
      expect(details('institutions')).toBe('NINA');
    });

    it('shows the id of an item that is not in the list', () => {
      flushLookups();
      filterState.addTaxonGroup(1);
      filterState.addTaxonGroup(99);
      expect(details('taxonGroups')).toBe('Fugler, #99');
    });

    it('names behaviors and basis of record as the filter panel does', () => {
      flushLookups();
      filterState.addBehavior(1);
      filterState.addBasisOfRecord(1);
      // Without loaded translations the name falls back to the description.
      expect(details('behaviors')).toBe('På land');
      expect(details('basisOfRecords')).toBe('Human Observation');
    });

    it('lists the categories of a type in red list order', () => {
      flushLookups();
      filterState.addCategory(11);
      filterState.addCategory(10);
      expect(details('categories:Rødliste')).toBe('Kritisk truet, Sterkt truet');
    });

    it('lists months in calendar order', () => {
      flushLookups();
      filterState.toggleMonth(6);
      filterState.toggleMonth(3);
      expect(details('months')).toBe('sidebar.months.march, sidebar.months.june');
    });

    it('names a whole county instead of its municipalities', () => {
      flushLookups();
      selectAllFinnmarkMunicipalities();
      expect(details('areas:mainland')).toBe('Finnmark');

      filterState.removeMunicipality('5601');
      const names = details('areas:mainland')!.split(', ');
      expect(names).toHaveLength(17);
      expect(names[0]).toBe('Kommune 2');
    });

    it('names Svalbard and ocean areas', () => {
      flushLookups();
      filterState.addCounty('21');
      filterState.toggleOceanArea('ocean1');
      expect(details('areas:svalbard')).toBe('Svalbard');
      expect(details('areas:ocean')).toBe('Norskehavet');
    });

    it('lists at most 20 names and counts the rest', () => {
      flushLookups();
      const translateSpy = vi.spyOn(translate, 'instant');
      filterState.setTaxons(Array.from({ length: 23 }, (_, i) => i + 1));
      expect(details('taxons')!.split(', ')).toHaveLength(20);
      expect(translateSpy).toHaveBeenCalledWith('sidebar.chipMoreNames', { count: 3 });
      fixture.detectChanges();
      httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry').flush([]);
    });

    it('names taxa once they have been looked up, following the scientific names switch', async () => {
      flushLookups();
      filterState.setTaxons([1234]);
      expect(details('taxons')).toBe('#1234');

      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url === '/api/Lookup/TaxonAncestry')
        .flush([{ id: 1234, parentIds: [], validScientificName: 'Cnidaria', preferredPopularName: 'nesledyr' }]);
      await fixture.whenStable();
      expect(details('taxons')).toBe('nesledyr');

      fixture.componentRef.setInput('showScientificNames', true);
      expect(details('taxons')).toBe('Cnidaria');
    });

    it('shows scientific names without their italic markup, and falls back from an empty name', async () => {
      flushLookups();
      filterState.setTaxons([1234]);
      fixture.detectChanges();
      httpTesting
        .expectOne((r) => r.url === '/api/Lookup/TaxonAncestry')
        .flush([{ id: 1234, parentIds: [], validScientificName: '<i>Betula pubescens</i>', preferredPopularName: '' }]);
      await fixture.whenStable();
      expect(details('taxons')).toBe('Betula pubescens');
    });

    it('shows a month outside the calendar by its number, so the filter is never invisible', () => {
      flushLookups();
      filterState.selectedMonths.set([13, 6]);
      expect(details('months')).toBe('sidebar.months.june, #13');
    });
  });

  describe('opening the filter', () => {
    it('asks for the section where the clicked chip is set', () => {
      flushLookups();
      filterState.addInstitution(5);
      filterState.setTaxons([1]);
      fixture.detectChanges();
      httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry').flush([]);

      const opened: string[] = [];
      component.openFilter.subscribe((section) => opened.push(section));
      fixture.nativeElement.querySelectorAll('.filter-chip-open').forEach((button: HTMLElement) => button.click());
      expect(opened).toEqual(['taxonTree', 'institutions']);
    });

    it('removes the filter without opening it', () => {
      flushLookups();
      filterState.addInstitution(5);
      fixture.detectChanges();
      const opened = vi.fn();
      component.openFilter.subscribe(opened);
      fixture.nativeElement.querySelector('.filter-chip adb-icon-button').click();
      expect(filterState.selectedInstitutionIds()).toEqual([]);
      expect(opened).not.toHaveBeenCalled();
    });

    it('gives each chip its own tooltip', () => {
      flushLookups();
      filterState.addInstitution(5);
      filterState.addTaxonGroup(1);
      fixture.detectChanges();
      const tooltips = fixture.debugElement.queryAll(By.directive(TooltipComponent));
      expect(new Set(tooltips.map((t) => t.nativeElement.id)).size).toBe(2);
    });
  });

  it('should update chip text when language changes', () => {
    flushLookups();
    filterState.addTaxonGroup(1);
    component.chips(); // trigger initial evaluation

    translate.use('en');
    fixture.detectChanges();

    const chipsAfter = component.chips();
    expect(chipsAfter.length).toBe(1);
    expect(chipsAfter[0]).toBeTruthy();
  });
});
