import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FilterStateService {
  readonly selectedCategoryIds = signal<number[]>([]);
  readonly selectedAreaIds = signal<string[]>([]);

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

  toggleArea(fid: string): void {
    this.selectedAreaIds.update((ids) =>
      ids.includes(fid) ? ids.filter((i) => i !== fid) : [...ids, fid],
    );
  }

  addArea(fid: string): void {
    this.selectedAreaIds.update((ids) => (ids.includes(fid) ? ids : [...ids, fid]));
  }

  removeArea(fid: string): void {
    this.selectedAreaIds.update((ids) => {
      if (!ids.includes(fid)) return ids;
      return ids.filter((i) => i !== fid);
    });
  }

  clearAreas(): void {
    this.selectedAreaIds.set([]);
  }

  clearAll(): void {
    this.clearCategories();
    this.clearAreas();
  }
}
