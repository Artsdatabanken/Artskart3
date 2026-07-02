import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { InstitutionDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class InstitutionService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Institutions';

  getInstitutions(): Observable<InstitutionDto[]> {
    return this.http.get<InstitutionDto[]>(this.endpoint);
  }
}
