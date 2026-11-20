import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { BehaviorDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class BehaviorService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Behaviors';
  private readonly behaviors$ = this.http.get<BehaviorDto[]>(this.endpoint).pipe(shareReplay(1));

  getBehaviors(): Observable<BehaviorDto[]> {
    return this.behaviors$;
  }
}

export function behaviorDisplayName(behavior: BehaviorDto, translate: (key: string) => string): string {
  if (!behavior.name) return behavior.description ?? '';
  const key = 'sidebar.behaviorName.' + behavior.name;
  const translated = translate(key);
  return translated !== key ? translated : (behavior.description ?? behavior.name);
}
