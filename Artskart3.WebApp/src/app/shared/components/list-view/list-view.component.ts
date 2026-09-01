import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationService } from '../../services/observation/observation.service';
import { AreaService } from '../../services/area/area.service';
import { CategoryService } from '../../services/category/category.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { FilterStateService, imageFilterToWithImages } from '../../services/filter-state/filter-state.service';
import { CategoryTypeDto, ObservationSearchFilter, PagedObservationResponse, TaxonGroupDto } from '../../types/api.types';
import { LocaleDatePipe } from '../../pipes/locale-date.pipe';
import { MeterUnitPipe } from '../../pipes/meter-unit.pipe';
import { LookupNamePipe } from '../../pipes/lookup-name.pipe';

@Component({
  selector: 'app-list-view',
  imports: [TranslateModule, LocaleDatePipe, MeterUnitPipe, LookupNamePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  protected readonly translate = inject(TranslateService);
  private readonly observationService = inject(ObservationService);
  private readonly areaService = inject(AreaService);
  private readonly categoryService = inject(CategoryService);
  private readonly taxonGroupService = inject(TaxonGroupService);
  private readonly filterState = inject(FilterStateService);

  private readonly categoriesResource = rxResource<CategoryTypeDto[], void>({
    stream: () => this.categoryService.getCategories(),
  });

  private readonly taxonGroupsResource = rxResource<TaxonGroupDto[], void>({
    stream: () => this.taxonGroupService.getTaxonGroups(),
  });

  readonly categoryNameMap = computed(() => {
    const map = new Map<number, string>();
    for (const type of this.categoriesResource.value() ?? []) {
      for (const cat of type.categories ?? []) {
        if (cat.id != null && cat.name) {
          map.set(cat.id, cat.name);
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

  readonly pageNumber = signal(1);

  private readonly _resetPageOnFilterChange = effect(() => {
    this.filterState.selectedCategoryIds();
    this.filterState.selectedMunicipalityIds();
    this.filterState.selectedCountyIds();
    this.filterState.selectedOceanAreaIds();
    this.filterState.selectedInstitutionIds();
    this.filterState.selectedBehaviorIds();
    this.filterState.selectedBasisOfRecordIds();
    this.filterState.selectedRegistrationStatusId();
    this.filterState.selectedTaxonGroupIds();
    this.filterState.coordinatePrecisionFrom();
    this.filterState.coordinatePrecisionTo();
    this.filterState.periodFrom();
    this.filterState.periodTo();
    this.filterState.projectName();
    this.filterState.projectOrganizationId();
    this.filterState.collectionCode();
    this.filterState.catalogNumber();
    this.filterState.imageFilter();
    this.filterState.selectedMonths();
    untracked(() => {
      if (this.pageNumber() !== 1) {
        this.pageNumber.set(1);
      }
    });
  });
  readonly pageSizeOptions = [10, 25, 50];
  readonly resultsPerPage = signal(this.pageSizeOptions[0]);

  private readonly searchParams = computed<ObservationSearchFilter>(
    () => {
      const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();
      const periodMonths = this.filterState.selectedMonths();
      const hasCoordinatePrecision = coordinatePrecisionFrom != null || coordinatePrecisionTo != null;
      const projectName = this.filterState.projectName().trim();
      const projectOrganizationId = this.filterState.projectOrganizationId();
      const collectionCode = this.filterState.collectionCode().trim();
      const catalogNumber = this.filterState.catalogNumber().trim();
      const withImages = imageFilterToWithImages(this.filterState.imageFilter());
      const hasPeriod = periodFrom != null || periodTo != null || periodMonths.length > 0;

      return {
        pageNumber: this.pageNumber(),
        resultsPerPage: this.resultsPerPage(),
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        registrationStatusId: this.filterState.selectedRegistrationStatusId() ?? undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        taxonIds: this.filterState.selectedTaxonIds().length ? this.filterState.selectedTaxonIds() : undefined,
        countyIds: countyIds.length ? countyIds : undefined,
        municipalityIds: municipalityIds.length ? municipalityIds : undefined,
        oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
        coordinatePrecision: hasCoordinatePrecision ? { from: coordinatePrecisionFrom, to: coordinatePrecisionTo } : undefined,
        projectName: projectName ? projectName : undefined,
        projectOrganizationId: projectOrganizationId ?? undefined,
        collectionCode: collectionCode ? collectionCode : undefined,
        catalogNumber: catalogNumber ? catalogNumber : undefined,
        withImages: withImages,
        period: hasPeriod
          ? { from: periodFrom, to: periodTo, months: periodMonths.length ? periodMonths : undefined }
          : undefined,
      };
    },
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

  getAreaName(fid: string | null | undefined): string {
    if (!fid) return '';
    return this.areaNameMap().get(fid) ?? fid;
  }
}
