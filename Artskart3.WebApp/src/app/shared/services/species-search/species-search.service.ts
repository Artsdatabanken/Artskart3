import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SpeciesDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class SpeciesSearchService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Search/Species';

  searchSpecies(search: string): Observable<SpeciesDto[]> {
    return this.http.get<SpeciesDto[]>(this.endpoint, { params: { search } });
  }
}
