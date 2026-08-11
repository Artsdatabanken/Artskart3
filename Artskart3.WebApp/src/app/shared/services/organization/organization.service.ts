import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { components } from '../../types/api.generated';
import { Observable } from 'rxjs';


@Injectable({
  providedIn: 'root',
})
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Organizations';

  searchOrganizations(search: string): Observable<components['schemas']['OrganizationDto'][]> {
    return this.http.get<components['schemas']['OrganizationDto'][]>(this.endpoint, { params: { search } });
  }
}
