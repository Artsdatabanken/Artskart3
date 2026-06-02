import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject, computed, effect, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { AsyncPipe } from '@angular/common';
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
  imports: [AsyncPipe, TranslateModule, PaginationComponent, LocaleDatePipe, MeterUnitPipe, AreaNamePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  private readonly observationService = inject(ObservationService);
  private readonly areaService = inject(AreaService);
  private readonly filterState = inject(FilterStateService);

  private readonly areaTypes = toSignal(this.areaService.getAreas(), { initialValue: [] });

  readonly pageNumber = signal(1);

  private readonly resetPageOnFilterChange = effect(() => {
    this.filterState.selectedCategoryIds();
    this.filterState.selectedMunicipalityIds();
    this.filterState.selectedCountyIds();
    untracked(() => {
      if (this.pageNumber() !== 1) {
        this.pageNumber.set(1);
      }
    });
  });
  readonly pageSizeOptions = [10, 25, 50];
  readonly resultsPerPage = signal(this.pageSizeOptions[0]);

  /**
   * Derives which county/municipality IDs to send to the API:
   * - If all municipalities under a county are selected → send county fid only
   * - If only some municipalities are selected → send those municipality fids only
   */
  private readonly areaFilter = computed(() => {
    const selectedMunicipalities = this.filterState.selectedMunicipalityIds();
    const areaTypes = this.areaTypes();

    const countyType = areaTypes.find((t) => t.name === 'County');
    const municipalityType = areaTypes.find((t) => t.name === 'Municipality');

    const counties = (countyType?.areas ?? []).filter((a) => a.fid && a.isCurrent);
    const allMunicipalities = (municipalityType?.areas ?? []).filter((a) => a.fid && a.isCurrent);

    const countyIds: string[] = [];
    const municipalityIds: string[] = [];

    for (const county of counties) {
      const countyMunicipalities = allMunicipalities.filter(
        (m) => m.fid!.substring(0, 2) === county.fid,
      );
      const selectedInCounty = countyMunicipalities.filter((m) =>
        selectedMunicipalities.includes(m.fid!),
      );

      if (selectedInCounty.length === 0) continue;

      if (selectedInCounty.length === countyMunicipalities.length) {
        countyIds.push(county.fid!);
      } else {
        selectedInCounty.forEach((m) => municipalityIds.push(m.fid!));
      }
    }

    return { countyIds, municipalityIds };
  });

  readonly observationsResource = rxResource<PagedObservationResponse, Partial<ObservationSearchFilter>>({
    params: () => ({
      pageNumber: this.pageNumber(),
      resultsPerPage: this.resultsPerPage(),
      risikokategoriIder: this.filterState.selectedCategoryIds(),
      countyIds: this.areaFilter().countyIds,
      municipalityIds: this.areaFilter().municipalityIds,
    }),
    stream: ({ params }) => {
      const filter: ObservationSearchFilter = {
        pageNumber: params.pageNumber ?? 1,
        resultsPerPage: params.resultsPerPage ?? 10,
        risikokategoriIder: params.risikokategoriIder?.length ? params.risikokategoriIder : undefined,
        countyIds: params.countyIds?.length ? params.countyIds : undefined,
        municipalityIds: params.municipalityIds?.length ? params.municipalityIds : undefined,
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
