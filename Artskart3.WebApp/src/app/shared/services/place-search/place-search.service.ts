import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PlaceSearchResultDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class PlaceSearchService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Search/SearchPlaces';

  searchPlaces(search: string): Observable<PlaceSearchResultDto[]> {
    return this.http.get<PlaceSearchResultDto[]>(this.endpoint, { params: { search } });
  }
}
