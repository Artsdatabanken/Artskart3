import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import '@artsdatabanken/components';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { ListViewComponent } from './list-view.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { MapViewService } from '../../services/map-view/map-view.service';
import { ObservationDto } from '../../types/api.types';
import { ListObservationDetailsComponent } from '../list-observation-details/list-observation-details.component';

describe('ListViewComponent', () => {
  let component: ListViewComponent;
  let fixture: ComponentFixture<ListViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ListViewComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(ListViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  async function respond(items: ObservationDto[]): Promise<HTMLAnchorElement> {
    const httpTesting = TestBed.inject(HttpTestingController);
    httpTesting.expectOne('/api/Lookup/Categories').flush([]);
    httpTesting.expectOne('/api/Lookup/TaxonGroups').flush([]);
    httpTesting.expectOne('/api/Lookup/Areas').flush({});
    httpTesting
      .expectOne('/api/Search/Observation')
      .flush({ items, pageNumber: 1, resultsPerPage: 10, lookaheadCount: 0, hasMorePages: false });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('.show-on-map');
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with page 1', () => {
    expect(component.pageNumber()).toBe(1);
  });

  it('should initialize with lowest page size option', () => {
    expect(component.resultsPerPage()).toBe(10);
  });

  it('should have page size options', () => {
    expect(component.pageSizeOptions).toEqual([10, 25, 50]);
  });

  it('renders the risk category badge with the category lookup code', async () => {
    const httpTesting = TestBed.inject(HttpTestingController);
    httpTesting
      .expectOne('/api/Lookup/Categories')
      .flush([{ id: 1, name: 'Rødliste', categories: [{ id: 10, code: 'CR', name: 'Kritisk truet' }] }]);
    httpTesting.expectOne('/api/Lookup/TaxonGroups').flush([]);
    httpTesting.expectOne('/api/Lookup/Areas').flush({});
    httpTesting.expectOne('/api/Search/Observation').flush({
      items: [{ id: 1, categoryId: 10 }],
      pageNumber: 1,
      resultsPerPage: 10,
      lookaheadCount: 0,
      hasMorePages: false,
    });

    await fixture.whenStable();
    fixture.detectChanges();

    const badge = fixture.debugElement.query(By.directive(RiskCategoryBadgeComponent));
    expect(badge).toBeTruthy();
    expect(badge.injector.get(RiskCategoryBadgeComponent).code()).toBe('CR');
  });

  describe('show on map link', () => {
    const observation: ObservationDto = { id: 42, point: { east: 262000, north: 6650000 }, coordinatePrecisionInMeters: 100 };

    it('links to the observation through the observationId query parameter', async () => {
      const link = await respond([observation]);

      expect(link.getAttribute('href')).toBe('/?observationId=42');
      link.click();
      await fixture.whenStable();

      expect(TestBed.inject(Router).url).toBe('/?observationId=42');
    });

    it('asks the map to show the observation with its coordinate precision and switches to it', async () => {
      const shown = vi.fn();
      component.observationShown.subscribe(shown);
      const link = await respond([observation]);

      link.click();

      expect(TestBed.inject(MapViewService).requestedExtent()).toEqual([257000, 6645000, 267000, 6655000]);
      expect(shown).toHaveBeenCalled();
    });

    it('leaves the map as it is when the observation has no usable coordinate', async () => {
      const shown = vi.fn();
      component.observationShown.subscribe(shown);
      const link = await respond([{ ...observation, point: undefined }]);

      link.click();

      expect(TestBed.inject(MapViewService).requestedExtent()).toBeNull();
      expect(shown).toHaveBeenCalled();
    });

    it('names the species and date for screen readers, falling back to the scientific name', async () => {
      TestBed.inject(TranslateService).use('no');
      await respond([
        {
          ...observation,
          preferredPopularName: 'munk',
          scientificName: '<i>Sylvia atricapilla</i>',
          dateTimeCollected: '2026-05-21T10:00:00',
        },
        { id: 43, scientificName: '<i>Emberiza schoeniclus</i>' },
      ]);

      const links: HTMLAnchorElement[] = Array.from(fixture.nativeElement.querySelectorAll('.show-on-map'));

      expect(links.map((link) => link.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
        'listView.showOnMap munk 21. mai 2026',
        'listView.showOnMap Emberiza schoeniclus',
      ]);
      expect(links[0].querySelector<HTMLElement>('.adb-sr-only [lang]')?.textContent?.trim()).toBe('munk');
      expect(links[0].querySelector<HTMLElement>('.adb-sr-only [lang]')?.lang).toBe('no');
    });

    it('marks the link text with the interface language inside the Norwegian table body', async () => {
      TestBed.inject(TranslateService).use('en');
      const link = await respond([observation]);

      expect(link.lang).toBe('en');
    });

    it('leaves this tab as it is when the link is opened in a new one', () => {
      const shown = vi.fn();
      component.observationShown.subscribe(shown);

      component.showOnMap(observation, new MouseEvent('click', { metaKey: true }));

      expect(TestBed.inject(MapViewService).requestedExtent()).toBeNull();
      expect(shown).not.toHaveBeenCalled();
    });
  });

  describe('observation details', () => {
    function detailsPanel(): ListObservationDetailsComponent | undefined {
      return fixture.debugElement.query(By.directive(ListObservationDetailsComponent))?.componentInstance;
    }

    function detailsButton(id: number): HTMLElement {
      return fixture.nativeElement.querySelector(`.show-details[data-observation-id="${id}"]`);
    }

    async function openDetails(id: number): Promise<void> {
      detailsButton(id).click();
      fixture.detectChanges();
      TestBed.inject(HttpTestingController).expectOne(`/api/observations/${id}`).flush({ id });
      await fixture.whenStable();
    }

    it('opens the observation beside the list, stepping through the rows on the page', async () => {
      await respond([{ id: 41 }, { id: 42 }, { id: 43 }]);
      expect(detailsPanel()).toBeUndefined();

      await openDetails(42);

      expect(detailsPanel()?.observationId()).toBe(42);
      expect(detailsPanel()?.observationIds()).toEqual([41, 42, 43]);
    });

    it('marks the row of the observation shown as current', async () => {
      await respond([{ id: 41 }, { id: 42 }]);
      const rows = (): (string | null)[] =>
        [...fixture.nativeElement.querySelectorAll('tbody tr')].map((row: HTMLElement) => row.getAttribute('aria-current'));
      expect(rows()).toEqual([null, null]);

      await openDetails(42);
      fixture.detectChanges();

      expect(rows()).toEqual([null, 'true']);
    });

    it('names the species and date for screen readers', async () => {
      TestBed.inject(TranslateService).use('no');
      await respond([{ id: 42, preferredPopularName: 'munk', dateTimeCollected: '2026-05-21T10:00:00' }]);

      expect(detailsButton(42).textContent?.replace(/\s+/g, ' ').trim()).toBe('listView.showDetails munk 21. mai 2026');
      expect(detailsButton(42).querySelector<HTMLElement>('.adb-sr-only [lang]')?.lang).toBe('no');
    });

    it('returns focus to the row of the last observation shown when closed', async () => {
      await respond([{ id: 41 }, { id: 42 }]);
      await openDetails(41);
      detailsPanel()?.observationId.set(42);
      fixture.detectChanges();
      TestBed.inject(HttpTestingController).expectOne('/api/observations/42').flush({ id: 42 });
      await fixture.whenStable();
      await customElements.whenDefined('adb-button');
      const rowButton = detailsButton(42).shadowRoot!.querySelector('button')!;
      const focus = vi.spyOn(rowButton, 'focus');

      detailsPanel()?.closed.emit();
      await fixture.whenStable();

      expect(detailsPanel()).toBeUndefined();
      expect(focus).toHaveBeenCalled();
    });
  });

  describe('onPageChange', () => {
    it('should update page number', () => {
      component.onPageChange(3);
      expect(component.pageNumber()).toBe(3);
    });
  });

  describe('onPageSizeChange', () => {
    it('should update results per page', () => {
      component.onPageSizeChange(25);
      expect(component.resultsPerPage()).toBe(25);
    });

    it('should reset page to 1', () => {
      component.onPageChange(5);
      component.onPageSizeChange(50);
      expect(component.pageNumber()).toBe(1);
    });
  });

  describe('totalVisiblePages', () => {
    it('should default to current page when no response', () => {
      expect(component.totalVisiblePages()).toBe(1);
    });
  });

  describe('hasMorePages', () => {
    it('should default to false when no response', () => {
      expect(component.hasMorePages()).toBe(false);
    });
  });

  describe('filter change resets page', () => {
    it('should reset pageNumber to 1 when category filter changes', () => {
      const filterState = TestBed.inject(FilterStateService);

      component.onPageChange(5);
      expect(component.pageNumber()).toBe(5);

      filterState.toggleCategory(1);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });

    it.each([
      ['a species', (state: FilterStateService) => state.addTaxon(42)],
      ['the identification filter', (state: FilterStateService) => state.setIdentificationFilter('uncertain')],
      ['the origin filter', (state: FilterStateService) => state.setOriginFilter('introduced')],
      ['a protected area', (state: FilterStateService) => state.addRestrictedArea('Naturbase VV00001234')],
    ])('should reset pageNumber to 1 when %s is picked', (_, change) => {
      const filterState = TestBed.inject(FilterStateService);
      component.onPageChange(4);

      change(filterState);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });

    it('should reset pageNumber to 1 when descendant taxa are left out of a species filter', () => {
      const filterState = TestBed.inject(FilterStateService);
      filterState.addTaxon(42);
      fixture.detectChanges();
      component.onPageChange(4);

      filterState.setIncludeDescendantTaxa(false);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });

    it('should stay on the page when only a display name arrives', () => {
      const filterState = TestBed.inject(FilterStateService);
      filterState.addProject({ id: 9, name: '' });
      fixture.detectChanges();
      component.onPageChange(4);

      filterState.setProjectName(9, 'Prosjekt 9');
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(4);
    });

    it('should stay on page 1 if already on page 1 when filter changes', () => {
      const filterState = TestBed.inject(FilterStateService);

      expect(component.pageNumber()).toBe(1);

      filterState.toggleCategory(2);
      fixture.detectChanges();

      expect(component.pageNumber()).toBe(1);
    });
  });
});
