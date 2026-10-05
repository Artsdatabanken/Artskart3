import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ObservationListInfoDto,
  ObservationSearchFilter,
  PagedObservationResponse
} from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class ObservationService {
  private readonly http = inject(HttpClient);
  private readonly SearchObservationEndpoint = '/api/Search/';

  searchObservations(filter: ObservationSearchFilter): Observable<PagedObservationResponse> {
    return this.http.post<PagedObservationResponse>(`${this.SearchObservationEndpoint}Observation`, filter);
  }

  getObservationByLocation(ids: number[], filter: ObservationSearchFilter): Observable<ObservationListInfoDto[]> {
    return this.http.post<ObservationListInfoDto[]>(`${this.SearchObservationEndpoint}ObservationList`, {Ids: ids, Filter: filter});
  }
}
