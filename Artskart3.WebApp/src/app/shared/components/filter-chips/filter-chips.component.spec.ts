import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FilterChipsComponent } from './filter-chips.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

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
  };

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
      filterState.addCounty('21');
      filterState.addMunicipality('5601');
      const chips = component.chips();
      expect(chips.find((c) => c.id === 'areas:mainland')!.suffix).toBe('(2)');
      expect(chips.find((c) => c.id === 'areas:svalbard')).toBeUndefined();
    });

    it('lar fastlands-chippen fjerne alle områdevalg når områdene ikke lastes', () => {
      httpTesting.expectOne('/api/Lookup/Areas').error(new ProgressEvent('error'));
      flushCategories();
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

    it('viser chip for prosjekt med navn og fjerner både navn og id', () => {
      flushLookups();
      filterState.setProjectName('Artsprosjektet');
      filterState.setProjectOrgId(42);
      const chip = component.chips().find((c) => c.id === 'project');
      expect(chip!.text).toContain('Artsprosjektet');
      chip!.clear();
      expect(filterState.projectOrgId()).toBeNull();
      expect(filterState.projectName()).toBe('');
    });

    it('viser ingen prosjektchip når id mangler (tekst uten valgt treff)', () => {
      flushLookups();
      filterState.setProjectName('Ufullstendig tekst');
      expect(component.chips().find((c) => c.id === 'project')).toBeUndefined();
    });

    it('viser chip for datasett og katalognummer', () => {
      flushLookups();
      filterState.setDatasetName('Datasett X');
      filterState.setDatasetOrgId(7);
      filterState.setCatalogNumber('ABC-123');
      filterState.setCatalogObservationIds([1, 2]);
      const chips = component.chips();
      expect(chips.find((c) => c.id === 'dataset')!.text).toContain('Datasett X');
      expect(chips.find((c) => c.id === 'catalogNumber')!.text).toContain('ABC-123');
    });
  });

  describe('radiochips', () => {
    it('viser ingen chip for standardvalg', () => {
      flushLookups();
      filterState.setRegistrationStatus(null);
      filterState.setImageFilter('all');
      expect(component.chips().find((c) => c.id === 'registrationStatus')).toBeUndefined();
      expect(component.chips().find((c) => c.id === 'imageFilter')).toBeUndefined();
    });

    it('viser chip med filtrets tittel og valgt radioknapp', () => {
      flushLookups();
      filterState.setRegistrationStatus(2);
      filterState.setImageFilter('withoutImage');
      const chips = component.chips();
      // Uten lastede oversettelser returnerer instant() nøklene
      expect(chips.find((c) => c.id === 'registrationStatus')!.text).toContain('sidebar.registreringStatus.absent');
      expect(chips.find((c) => c.id === 'imageFilter')!.text).toContain('sidebar.imageWithout');
    });

    it('tilbakestiller til standardvalg når chip lukkes', () => {
      flushLookups();
      filterState.setRegistrationStatus(2);
      filterState.setImageFilter('withoutImage');
      const chips = component.chips();
      chips.find((c) => c.id === 'registrationStatus')!.clear();
      chips.find((c) => c.id === 'imageFilter')!.clear();
      expect(filterState.selectedRegistrationStatusId()).toBeNull();
      expect(filterState.imageFilter()).toBe('all');
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
    it('har full tekst i title-attributtet', () => {
      flushLookups();
      filterState.setProjectName('Et prosjekt med et veldig langt navn som ikke får plass');
      filterState.setProjectOrgId(42);
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      const chip = el.querySelector('.filter-chip');
      expect(chip!.getAttribute('title')).toContain('Et prosjekt med et veldig langt navn som ikke får plass');
    });

    it('holder suffix i eget element slik at det ikke trunkeres', () => {
      flushLookups();
      filterState.addTaxonGroup(1);
      fixture.detectChanges();
      const el: HTMLElement = fixture.nativeElement;
      const suffix = el.querySelector('.filter-chip-suffix');
      expect(suffix!.textContent).toContain('(1)');
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
