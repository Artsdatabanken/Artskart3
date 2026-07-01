import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ObservationSearchFilter, PagedObservationResponse } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class ObservationService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Search/Observation';

  searchObservations(filter: ObservationSearchFilter): Observable<PagedObservationResponse> {
    return this.http.post<PagedObservationResponse>(this.endpoint, filter);
  }
}
