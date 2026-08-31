import { Injectable, signal } from '@angular/core';

export type ImageFilterOption = 'all' | 'withImage' | 'withoutImage';

export function imageFilterToWithImages(option: ImageFilterOption): boolean | undefined {
  if (option === 'withImage') return true;
  if (option === 'withoutImage') return false;
  return undefined;
}

@Injectable({
  providedIn: 'root',
})
export class FilterStateService {
  readonly selectedCategoryIds = signal<number[]>([]);
  readonly selectedCountyIds = signal<string[]>([]);
  readonly selectedMunicipalityIds = signal<string[]>([]);
  readonly selectedInstitutionIds = signal<number[]>([]);
  readonly selectedBehaviorIds = signal<number[]>([]);
  readonly selectedBasisOfRecordIds = signal<number[]>([]);
  readonly selectedRegistrationStatusId = signal<number | null>(null);
  readonly selectedTaxonGroupIds = signal<number[]>([]);
  readonly selectedOceanAreaIds = signal<string[]>([]);
  readonly selectedTaxonIds = signal<number[]>([]);
  readonly coordinatePrecisionFrom = signal<number | null>(null);
  readonly coordinatePrecisionTo = signal<number | null>(null);
  readonly periodFrom = signal<number | null>(null);
  readonly periodTo = signal<number | null>(null);
  // Samling, prosjekt og katalognummer filtreres på ID. Teksten beholdes kun for
  // å vise hva brukeren har valgt — det er ID-ene som sendes til backend.
  // Uten et valgt treff er ID-en null, og filteret er ikke aktivt.
  readonly datasetName = signal<string>('');
  readonly datasetOrgId = signal<number | null>(null);
  readonly collectionName = signal<string>('');
  readonly collectionOrgId = signal<number | null>(null);
  readonly catalogNumber = signal<string>('');
  readonly catalogObservationIds = signal<number[]>([]);
  readonly imageFilter = signal<ImageFilterOption>('all');
  readonly selectedMonths = signal<number[]>([]);

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

  toggleBasisOfRecord(id: number): void {
    this.selectedBasisOfRecordIds.update((ids) =>
      ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id],
    );
  }

  addBasisOfRecord(id: number): void {
    this.selectedBasisOfRecordIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }

  removeBasisOfRecord(id: number): void {
    this.selectedBasisOfRecordIds.update((ids) => {
      if (!ids.includes(id)) return ids;
      return ids.filter((i) => i !== id);
    });
  }

  clearBasisOfRecords(): void {
    this.selectedBasisOfRecordIds.set([]);
  }

  setRegistrationStatus(id: number | null): void {
    this.selectedRegistrationStatusId.set(id);
  }

  clearRegistrationStatus(): void {
    this.selectedRegistrationStatusId.set(null);
  }

  toggleTaxonGroup(id: number): void {
    this.selectedTaxonGroupIds.update((ids) =>
      ids.includes(id) ? ids.filter((i) => i !== id) : [...ids, id],
    );
  }

  addTaxonGroup(id: number): void {
    this.selectedTaxonGroupIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }

  removeTaxonGroup(id: number): void {
    this.selectedTaxonGroupIds.update((ids) => {
      if (!ids.includes(id)) return ids;
      return ids.filter((i) => i !== id);
    });
  }

  clearTaxonGroups(): void {
    this.selectedTaxonGroupIds.set([]);
  }

  toggleOceanArea(fid: string): void {
    this.selectedOceanAreaIds.update((ids) =>
      ids.includes(fid) ? ids.filter((i) => i !== fid) : [...ids, fid],
    );
  }

  clearOceanAreas(): void {
    this.selectedOceanAreaIds.set([]);
  }

  addTaxon(taxonId: number): void {
    this.selectedTaxonIds.update((ids) => (ids.includes(taxonId) ? ids : [...ids, taxonId]));
  }

  removeTaxon(taxonId: number): void {
    this.selectedTaxonIds.update((ids) => ids.filter((id) => id !== taxonId));
  }

  toggleTaxon(taxonId: number): void {
    this.selectedTaxonIds.update((ids) =>
      ids.includes(taxonId) ? ids.filter((id) => id !== taxonId) : [...ids, taxonId],
    );
  }

  clearTaxons(): void {
    this.selectedTaxonIds.set([]);
  }

  clearAreas(): void {
    this.selectedCountyIds.set([]);
    this.selectedMunicipalityIds.set([]);
    this.selectedOceanAreaIds.set([]);
  }

  setCoordinatePrecision(from: number | null, to: number | null): void {
    this.coordinatePrecisionFrom.set(from);
    this.coordinatePrecisionTo.set(to);
  }

  clearCoordinatePrecision(): void {
    this.coordinatePrecisionFrom.set(null);
    this.coordinatePrecisionTo.set(null);
  }

  setPeriod(from: number | null, to: number | null): void {
    this.periodFrom.set(from);
    this.periodTo.set(to);
  }

  toggleMonth(month: number): void {
    this.selectedMonths.update((months) =>
      months.includes(month) ? months.filter((m) => m !== month) : [...months, month],
    );
  }

  clearMonths(): void {
    this.selectedMonths.set([]);
  }

  clearPeriod(): void {
    this.periodFrom.set(null);
    this.periodTo.set(null);
    this.clearMonths();
  }

  setDatasetName(value: string): void {
    this.datasetName.set(value);
  }

  setDatasetOrgId(id: number | null): void {
    this.datasetOrgId.set(id);
  }

  setCollectionName(value: string): void {
    this.collectionName.set(value);
  }

  setCollectionOrgId(id: number | null): void {
    this.collectionOrgId.set(id);
  }

  setCatalogNumber(value: string): void {
    this.catalogNumber.set(value);
  }

  setCatalogObservationIds(ids: number[]): void {
    this.catalogObservationIds.set(ids);
  }

  setImageFilter(value: ImageFilterOption): void {
    this.imageFilter.set(value);
  }

  clearOtherFindProperties(): void {
    this.datasetName.set('');
    this.datasetOrgId.set(null);
    this.collectionName.set('');
    this.collectionOrgId.set(null);
    this.catalogNumber.set('');
    this.catalogObservationIds.set([]);
    this.imageFilter.set('all');
  }

  clearAll(): void {
    this.clearCategories();
    this.clearAreas();
    this.clearInstitutions();
    this.clearBehaviors();
    this.clearBasisOfRecords();
    this.clearRegistrationStatus();
    this.clearTaxonGroups();
    this.clearCoordinatePrecision();
    this.clearPeriod();
    this.clearTaxons();
    this.clearOtherFindProperties();
  }
}
