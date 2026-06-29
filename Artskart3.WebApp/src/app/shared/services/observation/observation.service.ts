import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {ObservationDto, ObservationSearchFilter, PagedObservationResponse} from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class ObservationService {
  private readonly http = inject(HttpClient);
  private readonly SearchObservationEndpoint = '/api/Search/Observation';
  private readonly ObservationControllerEndpoint = '/api/Observation';

  searchObservations(filter: ObservationSearchFilter): Observable<PagedObservationResponse> {
    return this.http.post<PagedObservationResponse>(this.SearchObservationEndpoint, filter);
  }

  getObservationByLocation(ids: number[]): Observable<ObservationDto[]> {
    return this.http.post<ObservationDto[]>(`${this.ObservationControllerEndpoint}`, ids);
  }
}
