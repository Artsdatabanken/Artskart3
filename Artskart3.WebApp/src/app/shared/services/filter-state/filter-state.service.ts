import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FilterStateService {
  readonly selectedCategoryIds = signal<number[]>([]);

  toggleCategory(id: number): void {
    this.selectedCategoryIds.update((ids) =>
      ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id],
    );
  }

  addCategory(id: number): void {
    this.selectedCategoryIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }

  removeCategory(id: number): void {
    this.selectedCategoryIds.update((ids) => {
      if (!ids.includes(id)) return ids;
      return ids.filter((i) => i !== id);
    });
  }

  clearCategories(): void {
    this.selectedCategoryIds.set([]);
  }
}
