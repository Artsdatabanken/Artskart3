import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { CategoryTypeDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Categories';

  // Delt strøm: sidebar, listevisning og filter-chips bruker alle de samme
  // kategoriene, og skal ikke utløse hver sin request.
  private readonly categories$ = this.http.get<CategoryTypeDto[]>(this.endpoint).pipe(
    catchError(() => of([])),
    shareReplay(1),
  );

  readonly categoryTypes = toSignal(this.categories$, { initialValue: [] });

  /** categoryId -> navnet på kategoritypen (f.eks. 'Rødliste'), brukt for å dele chips per seksjon. */
  readonly categoryTypeNameById = computed(() => {
    const map = new Map<number, string>();
    for (const type of this.categoryTypes()) {
      for (const category of type.categories ?? []) {
        if (category.id != null && type.name) map.set(category.id, type.name);
      }
    }
    return map;
  });

  getCategories(): Observable<CategoryTypeDto[]> {
    return this.categories$;
  }
}
