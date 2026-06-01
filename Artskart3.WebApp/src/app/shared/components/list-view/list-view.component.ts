import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject, computed, effect, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { AsyncPipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { ObservationService } from '../../services/observation/observation.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { ObservationSearchFilter, PagedObservationResponse } from '../../types/api.types';
import { PaginationComponent } from '../pagination/pagination.component';
import { LocaleDatePipe } from '../../pipes/locale-date.pipe';
import { AreaNamePipe } from '../../pipes/area-name.pipe';

@Component({
  selector: 'app-list-view',
  imports: [AsyncPipe, TranslateModule, PaginationComponent, LocaleDatePipe, AreaNamePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './list-view.component.html',
  styleUrl: './list-view.component.css',
})
export class ListViewComponent {
  private readonly observationService = inject(ObservationService);
  private readonly filterState = inject(FilterStateService);

  readonly pageNumber = signal(1);

  private readonly resetPageOnFilterChange = effect(() => {
    this.filterState.selectedCategoryIds();
    this.filterState.selectedAreaIds();
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
      risikokategoriIder: this.filterState.selectedCategoryIds(),
      municipalityIds: this.filterState.selectedAreaIds(),
    }),
    stream: ({ params }) => {
      const filter: ObservationSearchFilter = {
        pageNumber: params.pageNumber ?? 1,
        resultsPerPage: params.resultsPerPage ?? 10,
        risikokategoriIder: params.risikokategoriIder?.length ? params.risikokategoriIder : undefined,
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
