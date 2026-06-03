import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FilterStateService {
  readonly selectedCategoryIds = signal<number[]>([]);
  readonly selectedCountyIds = signal<string[]>([]);
  readonly selectedMunicipalityIds = signal<string[]>([]);
  readonly selectedInstitutionIds = signal<number[]>([]);
  readonly selectedBehaviorIds = signal<number[]>([]);
  readonly coordinatePrecisionFrom = signal<number | null>(null);
  readonly coordinatePrecisionTo = signal<number | null>(null);

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

  toggleInstitution(id: number): void {
    this.selectedInstitutionIds.update((ids) =>
      ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id],
    );
  }

  addInstitution(id: number): void {
    this.selectedInstitutionIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }

  removeInstitution(id: number): void {
    this.selectedInstitutionIds.update((ids) => {
      if (!ids.includes(id)) return ids;
      return ids.filter((i) => i !== id);
    });
  }

  clearInstitutions(): void {
    this.selectedInstitutionIds.set([]);
  }

  toggleBehavior(id: number): void {
    this.selectedBehaviorIds.update((ids) =>
      ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id],
    );
  }

  addBehavior(id: number): void {
    this.selectedBehaviorIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }

  removeBehavior(id: number): void {
    this.selectedBehaviorIds.update((ids) => {
      if (!ids.includes(id)) return ids;
      return ids.filter((i) => i !== id);
    });
  }

  clearBehaviors(): void {
    this.selectedBehaviorIds.set([]);
  }

  clearAreas(): void {
    this.selectedCountyIds.set([]);
    this.selectedMunicipalityIds.set([]);
  }

  setCoordinatePrecision(from: number | null, to: number | null): void {
    this.coordinatePrecisionFrom.set(from);
    this.coordinatePrecisionTo.set(to);
  }

  clearCoordinatePrecision(): void {
    this.coordinatePrecisionFrom.set(null);
    this.coordinatePrecisionTo.set(null);
  }

  clearAll(): void {
    this.clearCategories();
    this.clearAreas();
    this.clearInstitutions();
    this.clearBehaviors();
    this.clearCoordinatePrecision();
  }
}
