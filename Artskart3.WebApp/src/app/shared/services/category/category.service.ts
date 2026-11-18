import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { CategoryDto, CategoryTypeDto } from '../../types/api.types';
import { CATEGORY_ORDER } from '@shared/constants/category-order.const';

@Injectable({
  providedIn: 'root',
})
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Categories';

  private readonly categories$ = this.http.get<CategoryTypeDto[]>(this.endpoint).pipe(
    catchError(() => of([])),
    shareReplay(1),
  );

  readonly categoryTypes = toSignal(this.categories$, { initialValue: [] });

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

/** Categories in red list and alien species order, as the filter panel lists them. */
export function sortCategories(categories: readonly CategoryDto[] | null | undefined): CategoryDto[] {
  const rank = (category: CategoryDto) => {
    const index = category.code ? CATEGORY_ORDER.indexOf(category.code) : -1;
    return index === -1 ? Infinity : index;
  };
  return [...(categories ?? [])].sort((a, b) => rank(a) - rank(b));
}
