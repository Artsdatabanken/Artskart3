import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TaxonGroupDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class TaxonGroupService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/TaxonGroups';

  getTaxonGroups(): Observable<TaxonGroupDto[]> {
    return this.http.get<TaxonGroupDto[]>(this.endpoint);
  }
}
