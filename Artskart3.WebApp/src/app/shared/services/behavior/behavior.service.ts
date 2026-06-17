import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BehaviorDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class BehaviorService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Behaviors';

  getBehaviors(): Observable<BehaviorDto[]> {
    return this.http.get<BehaviorDto[]>(this.endpoint);
  }
}
