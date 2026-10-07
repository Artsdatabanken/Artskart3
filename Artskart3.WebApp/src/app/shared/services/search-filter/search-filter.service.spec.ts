import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { SearchFilterService } from './search-filter.service';
import { FilterStateService } from '../filter-state/filter-state.service';
import { ObservationSearchFilter } from '../../types/api.types';

/**
 * Filteret som eksporten sender, må være det samme som søket bruker.
 *
 * Eksporten bygget tidligere sitt eget filter, og det manglet `taxonIds` og
 * `registrationStatusId`. Symptomet var ikke bare feil innhold i filen: API-et
 * teller opp treffene med det samme filteret før eksporten startes, så et søk
 * nedfiltrert til én art ble talt som hele tabellen, traff radgrensen og ga
 * brukeren «Eksport ikke mulig».
 */
describe('SearchFilterService', () => {
  let service: SearchFilterService;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SearchFilterService);
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);

    // AreaService henter områdene ved opprettelse. Uten et svar står de tomme,
    // og det er greit her — testene handler om attributtfiltrene.
    httpTesting.expectOne('/api/Lookup/Areas').flush({});
  });

  afterEach(() => httpTesting.verify());

  it('tar med taxonIds', () => {
    filterState.setTaxons([1234, 5678]);

    expect(service.observationFilter().taxonIds).toEqual([1234, 5678]);
  });

  it('tar med registrationStatusId', () => {
    filterState.setRegistrationStatus(2);

    expect(service.observationFilter().registrationStatusId).toBe(2);
  });

  it('utelater tomme filtre i stedet for å sende tomme lister', () => {
    const filter = service.observationFilter();

    expect(filter.taxonIds).toBeUndefined();
    expect(filter.registrationStatusId).toBeUndefined();
    expect(filter.categoryIds).toBeUndefined();
  });

  /**
   * Vaktposten mot at det oppstår et nytt avvik: hvert felt brukeren kan sette i
   * filterpanelet skal komme med i filteret. Legges det til et nytt filter i
   * FilterStateService uten at det tas med her, feiler denne.
   */
  it('tar med hvert felt brukeren kan filtrere på', () => {
    filterState.selectedCategoryIds.set([1]);
    filterState.selectedInstitutionIds.set([2]);
    filterState.selectedBehaviorIds.set([3]);
    filterState.selectedBasisOfRecordIds.set([4]);
    filterState.setRegistrationStatus(2);
    filterState.selectedTaxonGroupIds.set([5]);
    filterState.setTaxons([6]);
    filterState.selectedOceanAreaIds.set(['7']);
    filterState.setCoordinatePrecision(10, 20);
    filterState.setPeriod(1990, 2000);
    filterState.toggleMonth(6);
    filterState.setDatasetOrgId(8);
    filterState.setProjectOrgId(9);
    filterState.setCatalogObservationIds([10]);
    filterState.setImageFilter('withImage');

    const filter = service.observationFilter();

    const expected: Partial<ObservationSearchFilter> = {
      categoryIds: [1],
      organizationIds: [2],
      behaviorIds: [3],
      basisOfRecordIds: [4],
      registrationStatusId: 2,
      taxonGroupIds: [5],
      taxonIds: [6],
      oceanAreaIds: ['7'],
      coordinatePrecision: { from: 10, to: 20 },
      datasetOrgId: 8,
      projectOrgId: 9,
      observationIds: [10],
      withImages: true,
      period: { from: 1990, to: 2000, months: [6] },
    };

    expect(filter).toMatchObject(expected);
  });
});

/**
 * Lagrede filtre lagres som det samme filteret som sendes til søket, og
 * `applyFilter` må kunne gjøre det om til filterpanelet igjen. Testene under er
 * vaktposten mot at de to kommer i utakt når det legges til nye filterfelt.
 */
describe('SearchFilterService.applyFilter', () => {
  let service: SearchFilterService;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  const AREAS = {
    counties: {
      areas: [
        { fid: '03', name: 'Oslo', isCurrent: true },
        { fid: '11', name: 'Rogaland', isCurrent: true },
      ],
    },
    municipalities: {
      areas: [
        { fid: '0301', name: 'Oslo', isCurrent: true },
        { fid: '1101', name: 'Eigersund', isCurrent: true },
        { fid: '1103', name: 'Stavanger', isCurrent: true },
      ],
    },
    svalbardBjørnøyaAndJanMayen: { areas: [{ fid: '21', name: 'Svalbard', isCurrent: true }] },
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SearchFilterService);
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);
    httpTesting.expectOne('/api/Lookup/Areas').flush(AREAS);
  });

  afterEach(() => httpTesting.verify());

  function flushNameLookups(): void {
    httpTesting.match((req) => req.url.startsWith('/api/Lookup/Organizations/')).forEach((req) => {
      const id = Number(req.request.url.split('/').pop());
      req.flush({ id, name: `Organisasjon ${id}` });
    });
    httpTesting
      .match('/api/Lookup/CatalogNumbers/ByObservationIds')
      .forEach((req) => req.flush({ catalogNumber: 'O-123', observationIds: req.request.body }));
  }

  it('gir samme filter tilbake når det bygges, brukes og bygges igjen', async () => {
    filterState.selectedMunicipalityIds.set(['0301', '1101']);
    filterState.selectedCountyIds.set(['21']);
    filterState.selectedOceanAreaIds.set(['h1']);
    filterState.selectedCategoryIds.set([1]);
    filterState.selectedInstitutionIds.set([2]);
    filterState.selectedBehaviorIds.set([3]);
    filterState.selectedBasisOfRecordIds.set([4]);
    filterState.setRegistrationStatus(2);
    filterState.selectedTaxonGroupIds.set([5]);
    filterState.setTaxons([6]);
    filterState.setCoordinatePrecision(10, 200);
    filterState.setPeriod(1980, 2000);
    filterState.toggleMonth(6);
    filterState.setDatasetOrgId(8);
    filterState.setProjectOrgId(9);
    filterState.setCatalogObservationIds([10, 11]);
    filterState.setImageFilter('withoutImage');
    const saved = service.observationFilter();

    filterState.clearAll();
    await service.applyFilter(saved);
    flushNameLookups();

    expect(service.observationFilter()).toEqual(saved);
    // Hele Oslo fylke sendes som fylke, men panelet krysser av for kommunene.
    expect(saved.countyIds).toEqual(['21', '03']);
    expect(filterState.selectedMunicipalityIds()).toEqual(expect.arrayContaining(['0301', '1101']));
    expect(filterState.selectedCountyIds()).toEqual(['21']);
    expect(filterState.datasetName()).toBe('Organisasjon 8');
    expect(filterState.projectName()).toBe('Organisasjon 9');
    expect(filterState.catalogNumber()).toBe('O-123');
  });

  /**
   * Typen krever hvert felt i filteret. Får ObservationSearchFilterDto et nytt
   * felt, kompilerer ikke testen før feltet er lagt til her — og da feiler den
   * til `applyFilter` også håndterer det.
   */
  it('håndterer hvert felt i filteret', async () => {
    type FilterFields = Required<Omit<ObservationSearchFilter, 'pageNumber' | 'resultsPerPage' | 'isPaginated' | 'restrictedAreaIds'>>;
    const sample: FilterFields = {
      taxonGroupIds: [1],
      taxonIds: [2],
      categoryIds: [3],
      organizationIds: [4],
      municipalityIds: ['1101'],
      countyIds: ['21'],
      oceanAreaIds: ['h1'],
      behaviorIds: [5],
      basisOfRecordIds: [6],
      registrationStatusId: 1,
      coordinatePrecision: { from: 0, to: 100 },
      period: { from: 1990, to: 2000, months: [5, 6] },
      datasetOrgId: 7,
      projectOrgId: 8,
      observationIds: [9],
      withImages: true,
    };

    await service.applyFilter(sample);
    flushNameLookups();

    expect(service.observationFilter()).toEqual(sample);
  });

  it('fjerner det som var satt fra før', async () => {
    filterState.selectedCategoryIds.set([99]);
    filterState.setImageFilter('withImage');

    await service.applyFilter({ taxonIds: [1] });

    expect(service.observationFilter()).toEqual(expect.objectContaining({ taxonIds: [1], categoryIds: undefined, withImages: undefined }));
  });

  it('overskriver ikke navnet hvis brukeren har byttet datasett mens oppslaget pågikk', async () => {
    await service.applyFilter({ datasetOrgId: 8 });
    filterState.setDatasetOrgId(12);
    filterState.setDatasetName('Valgt etterpå');

    flushNameLookups();

    expect(filterState.datasetName()).toBe('Valgt etterpå');
  });

  it('viser ID-en når navneoppslaget feiler, så filteret ikke blir usynlig', async () => {
    await service.applyFilter({ projectOrgId: 9, observationIds: [10, 11] });

    httpTesting.expectOne('/api/Lookup/Organizations/9').flush(null, { status: 404, statusText: 'Not Found' });
    httpTesting
      .expectOne('/api/Lookup/CatalogNumbers/ByObservationIds')
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(filterState.projectName()).toBe('#9');
    expect(filterState.catalogNumber()).toBe('#10 (+1)');
  });

  it('hasActiveFilter er false uten filter og true med', () => {
    expect(service.hasActiveFilter()).toBe(false);

    filterState.setImageFilter('withImage');

    expect(service.hasActiveFilter()).toBe(true);
  });
});
