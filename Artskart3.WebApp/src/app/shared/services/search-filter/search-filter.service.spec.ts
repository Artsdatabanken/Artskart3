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
