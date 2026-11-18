import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ObservationListInfoDto,
  ObservationDetailDto,
  ObservationSearchFilter,
  PagedObservationImageResponse,
  PagedObservationResponse,
} from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class ObservationService {
  private readonly http = inject(HttpClient);
  private readonly SearchObservationEndpoint = '/api/Search/';

  getDetail(id: number): Observable<ObservationDetailDto> {
    return this.http.get<ObservationDetailDto>(`/api/observations/${id}`);
  }

  findIdByOccurrenceId(occurrenceId: string): Observable<number> {
    return this.http.get<number>('/api/observations/by-occurrence-id', { params: { occurrenceId } });
  }

  searchObservations(filter: ObservationSearchFilter): Observable<PagedObservationResponse> {
    return this.http.post<PagedObservationResponse>(`${this.SearchObservationEndpoint}Observation`, filter);
  }

  searchObservationImages(filter: ObservationSearchFilter): Observable<PagedObservationImageResponse> {
    return this.http.post<PagedObservationImageResponse>(`${this.SearchObservationEndpoint}ObservationImages`, filter);
  }

  getObservationByLocation(ids: number[], filter: ObservationSearchFilter): Observable<ObservationListInfoDto[]> {
    return this.http.post<ObservationListInfoDto[]>(`${this.SearchObservationEndpoint}ObservationList`, { Ids: ids, Filter: filter });
  }
}
