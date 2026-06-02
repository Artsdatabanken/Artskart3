import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FilterStateService {
  readonly selectedCategoryIds = signal<number[]>([]);
  readonly selectedCountyIds = signal<string[]>([]);
  readonly selectedMunicipalityIds = signal<string[]>([]);

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

  toggleCounty(fid: string): void {
    this.selectedCountyIds.update((ids) =>
      ids.includes(fid) ? ids.filter((i) => i !== fid) : [...ids, fid],
    );
  }

  addCounty(fid: string): void {
    this.selectedCountyIds.update((ids) => (ids.includes(fid) ? ids : [...ids, fid]));
  }

  removeCounty(fid: string): void {
    this.selectedCountyIds.update((ids) => {
      if (!ids.includes(fid)) return ids;
      return ids.filter((i) => i !== fid);
    });
  }

  toggleMunicipality(fid: string): void {
    this.selectedMunicipalityIds.update((ids) =>
      ids.includes(fid) ? ids.filter((i) => i !== fid) : [...ids, fid],
    );
  }

  addMunicipality(fid: string): void {
    this.selectedMunicipalityIds.update((ids) => (ids.includes(fid) ? ids : [...ids, fid]));
  }

  removeMunicipality(fid: string): void {
    this.selectedMunicipalityIds.update((ids) => {
      if (!ids.includes(fid)) return ids;
      return ids.filter((i) => i !== fid);
    });
  }

  clearAreas(): void {
    this.selectedCountyIds.set([]);
    this.selectedMunicipalityIds.set([]);
  }

  clearAll(): void {
    this.clearCategories();
    this.clearAreas();
  }
}
