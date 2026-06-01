import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { AreaTypeDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class AreaService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Areas';

  private readonly areas$ = this.http.get<AreaTypeDto[]>(this.endpoint).pipe(shareReplay(1));

  getAreas(): Observable<AreaTypeDto[]> {
    return this.areas$;
  }
}
