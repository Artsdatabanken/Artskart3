import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { AreaService } from '../area/area.service';
import { OrganizationService } from '../organization/organization.service';
import { Artskart2LinkService } from './artskart2-link.service';

describe('Artskart2LinkService', () => {
  let service: Artskart2LinkService;
  const areaService = { searchAreas: vi.fn() };
  const organizationService = { searchCatalogNumbers: vi.fn(), getOrganization: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AreaService, useValue: areaService },
        { provide: OrganizationService, useValue: organizationService },
      ],
    });
    service = TestBed.inject(Artskart2LinkService);
  });

  /** Blocked: [2] is Artskart 2's default, so it adds nothing to the translated filter. */
  async function translate(source: object): Promise<{ filter: unknown; dropped: string[] }> {
    const translation = await service.translate({ Blocked: [2], ...source });
    return { filter: JSON.parse(translation!.filter), dropped: translation!.dropped };
  }

  it('returns null when the filter is not an object', async () => {
    expect(await service.translate([1, 2])).toBeNull();
    expect(await service.translate(null)).toBeNull();
  });

  it('returns an empty filter when nothing carries over, so the link still replaces the filter that was set', async () => {
    expect(await translate({ Style: 1 })).toEqual({ filter: {}, dropped: [] });
  });

  describe('areas', () => {
    it('translates Artskart 2 production area IDs through the area table', async () => {
      // Oslo municipality, Oslo county, a nature reserve, Bjørnøya (another code in Artskart 3) and an unknown ID.
      expect(await translate({ AreaIds: [2478, 2611, 660, 2451, 999999] })).toEqual({
        filter: { municipalityIds: ['301'], countyIds: ['3', '2105'], restrictedAreaIds: ['Naturbase VV00000670'] },
        dropped: ['AreaIds'],
      });
      expect(areaService.searchAreas).not.toHaveBeenCalled();
    });

    it('finds historical areas by name, type and code', async () => {
      areaService.searchAreas.mockReturnValue(
        of([
          { id: 3945, fid: '32', name: 'Akershus', areaTypeId: 2, isCurrent: true },
          { id: 8, fid: '2', name: 'Akershus', areaTypeId: 2, isCurrent: false },
        ]),
      );

      expect(await translate({ AreaIds: [2606] })).toEqual({ filter: { historicalAreaIds: [8] }, dropped: [] });
      expect(areaService.searchAreas).toHaveBeenCalledWith('Akershus');
    });

    it('drops a historical area Artskart 3 does not have', async () => {
      areaService.searchAreas.mockReturnValue(throwError(() => new Error('500')));

      expect(await translate({ TaxonIds: [1], AreaIds: [2606] })).toEqual({ filter: { taxonIds: [1] }, dropped: ['AreaIds'] });
    });
  });

  it('looks up catalogue numbers by exact match', async () => {
    organizationService.searchCatalogNumbers.mockImplementation((search: string) =>
      of(
        search === 'O-12'
          ? [
              { catalogNumber: 'O-123', observationIds: [9] },
              { catalogNumber: 'o-12', observationIds: [7, 8] },
            ]
          : [],
      ),
    );

    expect(await translate({ CatalogNumbers: ['O-12', 'missing'] })).toEqual({
      filter: { observationIds: [7, 8] },
      dropped: ['CatalogNumbers'],
    });
  });

  describe('collections', () => {
    const organizations: Record<number, number> = { 441: 2, 442: 2, 17216: 3, 655: 1 };

    beforeEach(() => {
      organizationService.getOrganization.mockImplementation((id: number) =>
        organizations[id] ? of({ id, name: `Org ${id}`, organizationTypeId: organizations[id] }) : throwError(() => new Error('404')),
      );
    });

    it('filters Artskart 2 collections as datasets and Artskart 2 datasets as projects', async () => {
      expect(await translate({ CollectionIds: [441, 442] })).toEqual({ filter: { datasetOrgIds: [441, 442] }, dropped: [] });
      expect(await translate({ CollectionIds: [17216] })).toEqual({ filter: { projectOrgIds: [17216] }, dropped: [] });
    });

    it('keeps the organisations it finds and names the filter when one is missing', async () => {
      expect(await translate({ CollectionIds: [441, 999] })).toEqual({ filter: { datasetOrgIds: [441] }, dropped: ['CollectionIds'] });
    });

    it('drops a mix of types, which Artskart 2 combined with OR and Artskart 3 would combine with AND', async () => {
      expect(await translate({ TaxonIds: [1], CollectionIds: [441, 17216] })).toEqual({
        filter: { taxonIds: [1] },
        dropped: ['CollectionIds'],
      });
    });

    it('drops institutions that would merge with InstitutionIds', async () => {
      expect(await translate({ InstitutionIds: [1], CollectionIds: [655] })).toEqual({
        filter: { organizationIds: [1] },
        dropped: ['CollectionIds'],
      });
    });
  });

  describe('import sources', () => {
    it('filters an import source on the datasets that hold exactly its observations', async () => {
      // Node 34 is "Vascular Plants, Field notes, Agder naturmuseum", whose only dataset is 441.
      expect(await translate({ NodeIds: [34, 1] })).toEqual({ filter: { datasetOrgIds: [441, 2] }, dropped: [] });
    });

    it('drops the filter when an import source shares its dataset with others', async () => {
      // Node 454 has 7 observations in a dataset of 1.36 million.
      expect(await translate({ TaxonIds: [1], NodeIds: [34, 454] })).toEqual({ filter: { taxonIds: [1] }, dropped: ['NodeIds'] });
    });

    it('drops the filter when the link already filters on datasets', async () => {
      organizationService.getOrganization.mockReturnValue(of({ id: 440, name: 'v', organizationTypeId: 2 }));

      expect(await translate({ CollectionIds: [440], NodeIds: [34] })).toEqual({ filter: { datasetOrgIds: [440] }, dropped: ['NodeIds'] });
    });
  });

  describe('loadBookmark', () => {
    let httpTesting: HttpTestingController;
    const url = (id: string) => `/api/Lookup/Artskart2Bookmarks/${id}`;

    beforeEach(() => (httpTesting = TestBed.inject(HttpTestingController)));
    afterEach(() => httpTesting.verify());

    it('returns the stored filter', async () => {
      const found = service.loadBookmark('45652bc7-5911-4918-ae64-0007e7969f73');
      httpTesting.expectOne(url('45652bc7-5911-4918-ae64-0007e7969f73')).flush({ TaxonIds: [1] });
      expect(await found).toEqual({ TaxonIds: [1] });
    });

    it('returns null only when the bookmark does not exist', async () => {
      const missing = service.loadBookmark('9c3c3030-1832-4d01-a01c-00112b3443b3');
      httpTesting.expectOne(url('9c3c3030-1832-4d01-a01c-00112b3443b3')).flush(null, { status: 404, statusText: 'Not Found' });
      expect(await missing).toBeNull();
    });

    it('passes on other errors, which are not a missing bookmark', async () => {
      const failing = service.loadBookmark('9c3c3030-1832-4d01-a01c-00112b3443b3');
      httpTesting.expectOne(url('9c3c3030-1832-4d01-a01c-00112b3443b3')).flush(null, { status: 500, statusText: 'Server Error' });
      await expect(failing).rejects.toBeTruthy();
    });
  });
});
