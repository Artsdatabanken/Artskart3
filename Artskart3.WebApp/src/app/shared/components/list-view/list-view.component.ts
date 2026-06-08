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

@Component({
  selector: 'app-list-view',
  imports: [TranslateModule, PaginationComponent, LocaleDatePipe, MeterUnitPipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  private readonly observationService = inject(ObservationService);
  private readonly areaService = inject(AreaService);
  private readonly filterState = inject(FilterStateService);

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

  readonly observationsResource = rxResource<PagedObservationResponse, ObservationSearchFilter>({
    params: () => {
      const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
      const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
      const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
      const periodFrom = this.filterState.periodFrom();
      const periodTo = this.filterState.periodTo();
      const hasCoordinatePrecision = coordinatePrecisionFrom != null || coordinatePrecisionTo != null;
      const hasPeriod = periodFrom != null || periodTo != null;

      return {
        pageNumber: this.pageNumber(),
        resultsPerPage: this.resultsPerPage(),
        categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
        organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
        behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
        basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
        taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
        countyIds: countyIds.length ? countyIds : undefined,
        municipalityIds: municipalityIds.length ? municipalityIds : undefined,
        oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
        coordinatePrecision: hasCoordinatePrecision ? { from: coordinatePrecisionFrom, to: coordinatePrecisionTo } : undefined,
        period: hasPeriod ? { from: periodFrom, to: periodTo } : undefined,
      };
    },
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
    return (response.lookaheadCount ?? 0) > 0;
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
