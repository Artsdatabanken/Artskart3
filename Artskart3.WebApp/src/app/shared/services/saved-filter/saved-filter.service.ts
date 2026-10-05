import { HttpClient } from '@angular/common/http';
import { Service, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, map, tap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { MapViewService } from '../map-view/map-view.service';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { CreateSavedFilterRequestDto, SavedFilterDto } from '../../types/api.types';

@Service()
export class SavedFilterService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly searchFilter = inject(SearchFilterService);
  private readonly mapView = inject(MapViewService);
  private readonly baseUrl = '/api/User/SavedFilters';

  readonly filtersResource = rxResource({
    params: () => (this.authService.isAuthenticated() ? true : undefined),
    stream: () => this.http.get<SavedFilterDto[]>(this.baseUrl),
  });

  readonly filters = computed(() => (this.filtersResource.hasValue() ? this.filtersResource.value() : []));
  readonly defaultFilter = computed(() => this.filters().find((f) => f.isDefault) ?? null);

  /** Henter et av brukerens egne filtre, eller null hvis det ikke finnes (lenger). */
  findById(id: string): Observable<SavedFilterDto | null> {
    return this.http.get<SavedFilterDto[]>(this.baseUrl).pipe(map((filters) => filters.find((f) => f.id === id) ?? null));
  }

  /** Lagrer filteret som er satt nå, sammen med kartutsnittet. */
  create(name: string, isDefault: boolean): Observable<SavedFilterDto> {
    const extent = this.mapView.currentExtent();
    const request: CreateSavedFilterRequestDto = {
      name,
      filter: this.searchFilter.observationFilter(),
      extent: extent ? { minX: extent[0], minY: extent[1], maxX: extent[2], maxY: extent[3] } : undefined,
      isDefault,
    };
    return this.http.post<SavedFilterDto>(this.baseUrl, request).pipe(tap(() => this.filtersResource.reload()));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(tap(() => this.filtersResource.reload()));
  }

  setDefault(id: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/default`, null).pipe(tap(() => this.filtersResource.reload()));
  }

  clearDefault(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/default`).pipe(tap(() => this.filtersResource.reload()));
  }

  /** Setter filteret i filterpanelet og ber kartet zoome til utsnittet som ble lagret. */
  async activate(savedFilter: SavedFilterDto): Promise<void> {
    await this.searchFilter.applyFilter(savedFilter.filter ?? {});
    const extent = savedFilter.extent;
    if (extent?.minX != null && extent.minY != null && extent.maxX != null && extent.maxY != null) {
      this.mapView.requestFit([extent.minX, extent.minY, extent.maxX, extent.maxY]);
    }
  }
}
