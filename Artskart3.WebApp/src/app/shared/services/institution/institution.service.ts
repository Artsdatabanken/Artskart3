import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { InstitutionDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class InstitutionService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Institutions';
  private readonly institutions$ = this.http.get<InstitutionDto[]>(this.endpoint).pipe(shareReplay(1));

  getInstitutions(): Observable<InstitutionDto[]> {
    return this.institutions$;
  }
}
