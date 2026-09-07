import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

export interface FilterChip {
  label: string;
  text: string;
  clear: () => void;
}

@Component({
  selector: 'app-filter-chips',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-chips.component.html',
  styleUrl: './filter-chips.component.css',
})
export class FilterChipsComponent {
  private readonly filterState = inject(FilterStateService);
  private readonly translate = inject(TranslateService);
  private readonly currentLang = signal(this.translate.currentLang || this.translate.defaultLang);

  constructor() {
    this.translate.onLangChange.pipe(takeUntilDestroyed()).subscribe((event) => {
      this.currentLang.set(event.lang);
    });
  }

  readonly chips = computed((): FilterChip[] => {
    this.currentLang();
    const chips: FilterChip[] = [];
    const taxonGroups = this.filterState.selectedTaxonGroupIds();
    if (taxonGroups.length > 0) {
      const label = this.translate.instant('sidebar.taxonGroups');
      chips.push({ label, text: `${label} (${taxonGroups.length})`, clear: () => this.filterState.clearTaxonGroups() });
    }
    const categories = this.filterState.selectedCategoryIds();
    if (categories.length > 0) {
      const label = this.translate.instant('sidebar.categories');
      chips.push({ label, text: `${label} (${categories.length})`, clear: () => this.filterState.clearCategories() });
    }
    const municipalities = this.filterState.selectedMunicipalityIds();
    const oceanAreas = this.filterState.selectedOceanAreaIds();
    const areaCount = municipalities.length + oceanAreas.length;
    if (areaCount > 0) {
      const label = this.translate.instant('sidebar.areas');
      chips.push({ label, text: `${label} (${areaCount})`, clear: () => this.filterState.clearAreas() });
    }
    const institutions = this.filterState.selectedInstitutionIds();
    if (institutions.length > 0) {
      const label = this.translate.instant('sidebar.institutions');
      chips.push({ label, text: `${label} (${institutions.length})`, clear: () => this.filterState.clearInstitutions() });
    }
    const behaviors = this.filterState.selectedBehaviorIds();
    if (behaviors.length > 0) {
      const label = this.translate.instant('sidebar.behaviors');
      chips.push({ label, text: `${label} (${behaviors.length})`, clear: () => this.filterState.clearBehaviors() });
    }
    const basisOfRecords = this.filterState.selectedBasisOfRecordIds();
    if (basisOfRecords.length > 0) {
      const label = this.translate.instant('sidebar.basisOfRecords');
      chips.push({ label, text: `${label} (${basisOfRecords.length})`, clear: () => this.filterState.clearBasisOfRecords() });
    }
    const taxons = this.filterState.selectedTaxonIds();
    if (taxons.length > 0) {
      const label = this.translate.instant('sidebar.species');
      chips.push({ label, text: `${label} (${taxons.length})`, clear: () => this.filterState.clearTaxons() });
    }
    const precFrom = this.filterState.coordinatePrecisionFrom();
    const precTo = this.filterState.coordinatePrecisionTo();
    if (precFrom != null || precTo != null) {
      const label = this.translate.instant('sidebar.coordinatePrecision');
      const from = precFrom ?? 0;
      const to = precTo != null ? String(precTo) : '∞';
      const text = this.translate.instant('sidebar.chipCoordinatePrecision', { from, to });
      chips.push({ label, text, clear: () => this.filterState.clearCoordinatePrecision() });
    }
    const periodFrom = this.filterState.periodFrom();
    const periodTo = this.filterState.periodTo();
    if (periodFrom != null || periodTo != null) {
      const label = this.translate.instant('sidebar.period');
      const from = periodFrom != null ? String(periodFrom) : '...';
      const to = periodTo != null ? String(periodTo) : '...';
      const text = this.translate.instant('sidebar.chipPeriod', { from, to });
      chips.push({ label, text, clear: () => this.filterState.clearPeriod() });
    }
    return chips;
  });
}
