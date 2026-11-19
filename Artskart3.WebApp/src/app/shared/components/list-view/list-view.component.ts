import {
  afterNextRender,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  ElementRef,
  Injector,
  signal,
  inject,
  computed,
  linkedSignal,
  input,
  output,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationService } from '../../services/observation/observation.service';
import { AreaService } from '../../services/area/area.service';
import { CategoryService } from '../../services/category/category.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { SearchFilterService } from '../../services/search-filter/search-filter.service';
import { MapViewService } from '../../services/map-view/map-view.service';
import {
  CategoryDto,
  CategoryTypeDto,
  ObservationDto,
  ObservationSearchFilter,
  PagedObservationResponse,
  TaxonGroupDto,
} from '../../types/api.types';
import { LocaleDatePipe } from '../../pipes/locale-date.pipe';
import { MeterUnitPipe } from '../../pipes/meter-unit.pipe';
import { LookupNamePipe } from '../../pipes/lookup-name.pipe';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { ListObservationDetailsComponent } from '../list-observation-details/list-observation-details.component';

@Component({
  selector: 'app-list-view',
  imports: [
    TranslateModule,
    RouterLink,
    NgTemplateOutlet,
    LocaleDatePipe,
    MeterUnitPipe,
    LookupNamePipe,
    RiskCategoryBadgeComponent,
    ListObservationDetailsComponent,
  ],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  protected readonly translate = inject(TranslateService);
  private readonly observationService = inject(ObservationService);
  private readonly areaService = inject(AreaService);
  private readonly categoryService = inject(CategoryService);
  private readonly taxonGroupService = inject(TaxonGroupService);
  private readonly searchFilter = inject(SearchFilterService);
  private readonly mapView = inject(MapViewService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly active = input(true);

  /** Needed besides the URL change, which doesn't happen when the observation is already open. */
  readonly observationShown = output<void>();

  private readonly categoriesResource = rxResource<CategoryTypeDto[], void>({
    stream: () => this.categoryService.getCategories(),
  });

  private readonly taxonGroupsResource = rxResource<TaxonGroupDto[], void>({
    stream: () => this.taxonGroupService.getTaxonGroups(),
  });

  readonly categoryById = computed(() => {
    const map = new Map<number, CategoryDto>();
    for (const type of this.categoriesResource.value() ?? []) {
      for (const category of type.categories ?? []) {
        if (category.id !== undefined) {
          map.set(category.id, category);
        }
      }
    }
    return map;
  });

  readonly taxonGroupNameMap = computed(() => {
    const map = new Map<number, string>();
    for (const group of this.taxonGroupsResource.value() ?? []) {
      if (group.id != null && group.name) {
        map.set(group.id, group.name);
      }
    }
    return map;
  });

  readonly areaNameMap = computed(() => {
    const map = new Map<string, string>();
    for (const m of this.areaService.municipalities()) {
      map.set(m.fid, m.name ?? m.fid);
    }
    for (const c of this.areaService.counties()) {
      map.set(c.fid, c.name ?? c.fid);
    }
    return map;
  });

  // Back to page 1 whenever the filter changes.
  readonly pageNumber = linkedSignal(() => {
    this.searchFilter.observationFilter();
    return 1;
  });
  readonly pageSizeOptions = [10, 25, 50];
  readonly resultsPerPage = signal(this.pageSizeOptions[0]);

  /**
   * Selve filteret bygges i SearchFilterService, som deles med eksporten. Her
   * legges kun pagineringen på — den er unik for listevisningen.
   */
  private readonly searchParams = computed<ObservationSearchFilter>(
    () => ({
      ...this.searchFilter.observationFilter(),
      pageNumber: this.pageNumber(),
      resultsPerPage: this.resultsPerPage(),
    }),
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  readonly observationsResource = rxResource<PagedObservationResponse, ObservationSearchFilter>({
    params: () => this.searchParams(),
    stream: ({ params }) => this.observationService.searchObservations(params),
  });

  readonly totalVisiblePages = computed(() => {
    const response = this.observationsResource.value();
    const lookahead = response?.lookaheadCount ?? 0;
    return this.pageNumber() + lookahead;
  });

  readonly openObservationId = signal<number | null>(null);

  readonly pageObservationIds = computed(() =>
    this.observationsResource.hasValue() ? (this.observationsResource.value().items ?? []).flatMap((obs) => obs.id ?? []) : [],
  );

  readonly hasMorePages = computed(() => {
    const response = this.observationsResource.value();
    if (!response) return false;
    return response.hasMorePages ?? false;
  });

  onPageChange(page: number) {
    this.pageNumber.set(page);
  }

  onPageSizeChange(size: number) {
    this.resultsPerPage.set(size);
    this.pageNumber.set(1);
  }

  openDetails(observation: ObservationDto): void {
    if (observation.id != null) this.openObservationId.set(observation.id);
  }

  closeDetails(): void {
    const id = this.openObservationId();
    this.openObservationId.set(null);
    afterNextRender(
      () => {
        const host = this.host.nativeElement;
        const button = host.querySelector(`.show-details[data-observation-id="${id}"]`) ?? host.querySelector('.show-details');
        button?.shadowRoot?.querySelector('button')?.focus();
      },
      { injector: this.injector },
    );
  }

  showOnMap(observation: ObservationDto, event: MouseEvent): void {
    // A modified click opens the link in a new tab or window, so this one stays as it is.
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const { east, north } = observation.point ?? {};
    if (east != null && north != null) this.mapView.requestFitPoint(east, north, observation.coordinatePrecisionInMeters);
    this.observationShown.emit();
  }

  getAreaName(fid: string | null | undefined): string {
    if (!fid) return '';
    return this.areaNameMap().get(fid) ?? fid;
  }

  getCategory(id: number | null | undefined): CategoryDto | undefined {
    if (id === null || id === undefined) return undefined;
    return this.categoryById().get(id);
  }
}
