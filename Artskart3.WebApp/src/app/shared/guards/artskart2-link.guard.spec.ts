import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AreaService } from '../services/area/area.service';
import { Artskart2LinkService } from '../services/artskart2-link/artskart2-link.service';
import { FilterLinkService } from '../services/filter-link/filter-link.service';
import { OrganizationService } from '../services/organization/organization.service';
import { artskart2AppRedirect, artskart2LinkRouteGuards } from './artskart2-link.guard';

@Component({ template: '' })
class HomeStubComponent {}

describe('artskart2LinkGuard', () => {
  let harness: RouterTestingHarness;
  let router: Router;
  let links: Artskart2LinkService;
  const filterLinks = { startLink: vi.fn(), carry: vi.fn(), warnUnreadable: vi.fn(), warn: vi.fn() };

  beforeEach(async () => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: HomeStubComponent, ...artskart2LinkRouteGuards },
          { path: 'app', redirectTo: artskart2AppRedirect },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FilterLinkService, useValue: filterLinks },
        { provide: AreaService, useValue: {} },
        { provide: OrganizationService, useValue: {} },
      ],
    });
    harness = await RouterTestingHarness.create();
    router = TestBed.inject(Router);
    links = TestBed.inject(Artskart2LinkService);
  });

  /** Builds the link the way Artskart 2 did, with the filter JSON URL-encoded. */
  const artskart2Link = (filter: object, base = '/app/') =>
    `${base}#map/427864,7623020/3/background/NiB/filter/${encodeURIComponent(JSON.stringify(filter))}`;

  const queryParams = () => router.parseUrl(router.url).queryParams;

  function filterParam(): unknown {
    const filter = queryParams()['filter'];
    return filter === undefined ? undefined : JSON.parse(filter);
  }

  describe('observation links', () => {
    it('turns a featureInfo link into an occurrenceId link', async () => {
      await harness.navigateByUrl('/#featureInfo/nina%2Fskandobs%2F0008ef28-6c98-4791-b3f6-914b3908e4a3');
      const url = router.parseUrl(router.url);
      expect(url.queryParams).toEqual({ occurrenceId: 'nina/skandobs/0008ef28-6c98-4791-b3f6-914b3908e4a3' });
      expect(url.fragment).toBeNull();
      expect(filterLinks.startLink).not.toHaveBeenCalled();
    });

    it('also redirects when only the fragment changes on an open page', async () => {
      await harness.navigateByUrl('/?observationId=42');
      await harness.navigateByUrl('/#featureInfo/urn%3Acatalog%3AO%3AL%3A37');
      expect(queryParams()).toEqual({ occurrenceId: 'urn:catalog:O:L:37' });
    });

    it('turns a featureInfo link under /app/ into an occurrenceId link', async () => {
      await harness.navigateByUrl('/app/#featureInfo/urn%3Acatalog%3AO%3AL%3A37');

      expect(queryParams()).toEqual({ occurrenceId: 'urn:catalog:O:L:37' });
    });

    it('treats an annotation link like a featureInfo link', async () => {
      await harness.navigateByUrl('/app/#annotation/urn%3Acatalog%3AO%3AL%3A37');

      expect(queryParams()).toEqual({ occurrenceId: 'urn:catalog:O:L:37' });
    });

    it('opens an observationInfo link by observation ID', async () => {
      await harness.navigateByUrl('/app/#observationInfo/1000000');

      expect(router.url).toBe('/?observationId=1000000');
    });
  });

  it('leaves other URLs alone', async () => {
    await harness.navigateByUrl('/?observationId=42#other');
    expect(router.url).toBe('/?observationId=42#other');
    await harness.navigateByUrl('/#featureInfo/');
    expect(router.parseUrl(router.url).fragment).toBe('featureInfo/');
  });

  it('keeps the query string when /app is opened without an Artskart 2 link', async () => {
    await harness.navigateByUrl('/app?observationId=42');

    expect(router.url).toBe('/?observationId=42');
  });

  describe('filter links', () => {
    it('turns an Artskart 2 filter link under /app/ into a filter link, as a new link', async () => {
      await harness.navigateByUrl(artskart2Link({ TaxonIds: [26016], Found: [2], NotRecovered: [2], Blocked: [2], Style: 1 }));

      expect(router.parseUrl(router.url).root.children['primary']).toBeUndefined();
      expect(router.parseUrl(router.url).fragment).toBeNull();
      expect(filterParam()).toEqual({ taxonIds: [26016], registrationStatusId: 1 });
      expect(filterLinks.startLink).toHaveBeenCalled();
      expect(filterLinks.carry).toHaveBeenCalledWith([]);
    });

    it('also accepts Artskart 2 links without /app/ and from the other views', async () => {
      await harness.navigateByUrl(`/#list/427864,7623020/3/filter/${encodeURIComponent('{"TaxonIds":[1]}')}/session/abc`);

      expect(filterParam()).toEqual({ taxonIds: [1], hiddenObservations: 'Include' });
    });

    it('opens the observation selected in an Artskart 2 map link', async () => {
      await harness.navigateByUrl(
        `/#map/427864,7623020/3/background/NiB/id/1000000/filter/${encodeURIComponent('{"TaxonIds":[1],"Blocked":[2]}')}`,
      );

      expect(queryParams()).toEqual({ filter: '{"taxonIds":[1]}', observationId: '1000000' });
    });

    it('keeps the map area of an Artskart 2 link that moved the map', async () => {
      await harness.navigateByUrl(`/app/#map/262000,6650000/10/background/NiB/filter/${encodeURIComponent('{"TaxonIds":[1],"Blocked":[2]}')}`);

      expect(queryParams()).toEqual({ filter: '{"taxonIds":[1]}', extent: '248460,6641538,275540,6658463' });
    });

    it('keeps the map area of an Artskart 2 link without a filter', async () => {
      await harness.navigateByUrl('/app/#map/262000,6650000/10/background/NiB');

      expect(queryParams()['extent']).toBe('248460,6641538,275540,6658463');
      expect(filterParam()).toEqual({ registrationStatusId: 1 });
    });

    it('hands the Artskart 2 filters it dropped to the filter link', async () => {
      await harness.navigateByUrl(artskart2Link({ TaxonIds: [1], Blocked: [2], Errors: [2], Valid: [1] }));

      expect(filterParam()).toEqual({ taxonIds: [1] });
      expect(filterLinks.carry).toHaveBeenCalledWith(['Errors', 'Valid']);
    });

    it("gives a link without a filter Artskart 2's default view, replacing the filter that was set", async () => {
      await harness.navigateByUrl('/app/#map/427864,7623020/3/background/NiB');

      expect(filterParam()).toEqual({ registrationStatusId: 1 });
    });

    it('warns and leaves the filter alone when the link filter is not JSON', async () => {
      await harness.navigateByUrl('/app/#map/427864,7623020/3/filter/%7Bbroken');

      expect(router.url).toBe('/');
      expect(filterLinks.warnUnreadable).toHaveBeenCalled();
      expect(filterLinks.carry).not.toHaveBeenCalled();
    });

    it('filters a dataset page link on the datasets of its import source', async () => {
      const translate = vi.spyOn(links, 'translate').mockResolvedValue({ filter: '{"datasetOrgIds":[441]}', dropped: [] });

      await harness.navigateByUrl('/app/#/dataSet/0efbb3b3-6473-414a-af91-cdbcbcd8aba1/34');

      expect(translate).toHaveBeenCalledWith({ NodeIds: [34], Found: [2], NotRecovered: [2] });
      expect(filterParam()).toEqual({ datasetOrgIds: [441] });
    });

    it('asks for a reload when the translation fails, typically a table that no longer loads after a deploy', async () => {
      vi.spyOn(links, 'translate').mockRejectedValue(new Error('Failed to fetch dynamically imported module'));

      await harness.navigateByUrl(artskart2Link({ TaxonIds: [1] }));

      expect(router.url).toBe('/');
      expect(filterLinks.warn).toHaveBeenCalledWith('filterLink.failed');
    });
  });

  describe('bookmarks', () => {
    const bookmarkUrl = '/app/#bookmark/45652bc7-5911-4918-ae64-0007e7969f73';

    it('translates a stored Artskart 2 bookmark like a filter link', async () => {
      const bookmark = { Id: '45652bc7-5911-4918-ae64-0007e7969f73', TaxonIds: [26016], Found: [2], NotRecovered: [2], Blocked: [2] };
      const loadBookmark = vi.spyOn(links, 'loadBookmark').mockResolvedValue(bookmark);

      await harness.navigateByUrl(bookmarkUrl);

      expect(loadBookmark).toHaveBeenCalledWith('45652bc7-5911-4918-ae64-0007e7969f73');
      expect(filterParam()).toEqual({ taxonIds: [26016], registrationStatusId: 1 });
    });

    it('says so when the bookmark no longer exists', async () => {
      vi.spyOn(links, 'loadBookmark').mockResolvedValue(null);

      await harness.navigateByUrl(bookmarkUrl);

      expect(router.url).toBe('/');
      expect(filterLinks.warn).toHaveBeenCalledWith('filterLink.bookmarkNotFound');
    });

    it('asks for a reload, not "no longer exists", when the bookmark cannot be loaded', async () => {
      vi.spyOn(links, 'loadBookmark').mockRejectedValue(new Error('500'));

      await harness.navigateByUrl(bookmarkUrl);

      expect(router.url).toBe('/');
      expect(filterLinks.warn).toHaveBeenCalledWith('filterLink.failed');
      expect(filterLinks.warn).not.toHaveBeenCalledWith('filterLink.bookmarkNotFound');
    });
  });
});
