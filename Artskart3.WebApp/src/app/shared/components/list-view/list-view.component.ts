import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { ObservationService } from '../../services/observation/observation.service';
import { AreaService } from '../../services/area/area.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { ObservationSearchFilter, PagedObservationResponse } from '../../types/api.types';
import { PaginationComponent } from '../pagination/pagination.component';
import { LocaleDatePipe } from '../../pipes/locale-date.pipe';
import { MeterUnitPipe } from '../../pipes/meter-unit.pipe';
import { AreaNamePipe } from '../../pipes/area-name.pipe';

@Component({
  selector: 'app-list-view',
  imports: [TranslateModule, PaginationComponent, LocaleDatePipe, MeterUnitPipe, AreaNamePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  private readonly observationService = inject(ObservationService);
  private readonly areaService = inject(AreaService);
  private readonly filterState = inject(FilterStateService);

  readonly pageNumber = signal(1);

  private readonly _resetPageOnFilterChange = effect(() => {
    this.filterState.selectedCategoryIds();
    this.filterState.selectedMunicipalityIds();
    this.filterState.selectedCountyIds();
    this.filterState.selectedOceanAreaIds();
    this.filterState.selectedInstitutionIds();
    this.filterState.selectedBehaviorIds();
    this.filterState.selectedBasisOfRecordIds();
    this.filterState.selectedTaxonGroupIds();
    this.filterState.coordinatePrecisionFrom();
    this.filterState.coordinatePrecisionTo();
    this.filterState.periodFrom();
    this.filterState.periodTo();
    untracked(() => {
      if (this.pageNumber() !== 1) {
        this.pageNumber.set(1);
      }
    });
  });
  readonly pageSizeOptions = [10, 25, 50];
  readonly resultsPerPage = signal(this.pageSizeOptions[0]);

  readonly observationsResource = rxResource<PagedObservationResponse, Partial<ObservationSearchFilter>>({
    params: () => ({
      pageNumber: this.pageNumber(),
      resultsPerPage: this.resultsPerPage(),
      categoryIds: this.filterState.selectedCategoryIds(),
      organizationIds: this.filterState.selectedInstitutionIds(),
      behaviorIds: this.filterState.selectedBehaviorIds(),
      basisOfRecordIds: this.filterState.selectedBasisOfRecordIds(),
      taxonGroupIds: this.filterState.selectedTaxonGroupIds(),
      countyIds: this.areaService.resolvedAreaFilter().countyIds,
      municipalityIds: this.areaService.resolvedAreaFilter().municipalityIds,
      oceanAreaIds: this.filterState.selectedOceanAreaIds(),
      coordinatePrecision: {
        from: this.filterState.coordinatePrecisionFrom(),
        to: this.filterState.coordinatePrecisionTo(),
      },
      period: {
        from: this.filterState.periodFrom(),
        to: this.filterState.periodTo(),
      },
    }),
    stream: ({ params }) => {
      const hasCoordinatePrecision =
        params.coordinatePrecision?.from != null || params.coordinatePrecision?.to != null;
      const hasPeriod =
        params.period?.from != null || params.period?.to != null;
      const filter: ObservationSearchFilter = {
        pageNumber: params.pageNumber ?? 1,
        resultsPerPage: params.resultsPerPage ?? 10,
        categoryIds: params.categoryIds?.length ? params.categoryIds : undefined,
        organizationIds: params.organizationIds?.length ? params.organizationIds : undefined,
        behaviorIds: params.behaviorIds?.length ? params.behaviorIds : undefined,
        basisOfRecordIds: params.basisOfRecordIds?.length ? params.basisOfRecordIds : undefined,
        taxonGroupIds: params.taxonGroupIds?.length ? params.taxonGroupIds : undefined,
        countyIds: params.countyIds?.length ? params.countyIds : undefined,
        municipalityIds: params.municipalityIds?.length ? params.municipalityIds : undefined,
        oceanAreaIds: params.oceanAreaIds?.length ? params.oceanAreaIds : undefined,
        coordinatePrecision: hasCoordinatePrecision ? params.coordinatePrecision : undefined,
        period: hasPeriod ? params.period : undefined,
      };
      return this.observationService.searchObservations(filter);
    },
  });

  readonly totalVisiblePages = computed(() => {
    const response = this.observationsResource.value();
    const lookahead = response?.lookaheadCount ?? 0;
    return this.pageNumber() + lookahead;
  });

  readonly hasMorePages = computed(() => {
    const response = this.observationsResource.value();
    if (!response) return false;
    return (response.lookaheadCount ?? 0) > 0;
  });

  onPageChange(page: number) {
    this.pageNumber.set(page);
  }

  onPageSizeChange(size: number) {
    this.resultsPerPage.set(size);
    this.pageNumber.set(1);
  }
}
