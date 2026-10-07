import { Component, CUSTOM_ELEMENTS_SCHEMA, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { AreaService } from '../../services/area/area.service';
import { CategoryService } from '../../services/category/category.service';
import { REGISTRATION_STATUS_OPTIONS } from '@shared/constants/registration-status-options.const';

export interface FilterChip {
  id: string;
  label: string;
  text: string;
  suffix?: string;
  clear: () => void;
}

@Component({
  selector: 'app-filter-chips',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './filter-chips.component.html',
  styleUrl: './filter-chips.component.css',
})
export class FilterChipsComponent {
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly categoryService = inject(CategoryService);
  private readonly translate = inject(TranslateService);
  private readonly currentLang = signal(this.translate.getCurrentLang() || this.translate.getCurrentLang());

  constructor() {
    this.translate.onLangChange.pipe(takeUntilDestroyed()).subscribe((event) => {
      this.currentLang.set(event.lang);
    });
  }

  readonly chips = computed((): FilterChip[] => {
    this.currentLang();
    const t = (key: string, params?: Record<string, unknown>) => this.translate.instant(key, params);
    const chips: FilterChip[] = [];

    const countChip = (id: string, titleKey: string, count: number, clear: () => void): FilterChip => {
      const text = t(titleKey);
      const suffix = `(${count})`;
      return { id, text, suffix, label: `${text} ${suffix}`, clear };
    };

    const namedChip = (id: string, titleKey: string, value: string, clear: () => void): FilterChip => {
      const text = `${t(titleKey)}: ${value}`;
      return { id, text, label: text, clear };
    };

    const taxonGroups = this.filterState.selectedTaxonGroupIds();
    if (taxonGroups.length > 0) {
      chips.push(countChip('taxonGroups', 'sidebar.taxonGroups', taxonGroups.length, () => this.filterState.clearTaxonGroups()));
    }

    const taxons = this.filterState.selectedTaxonIds();
    if (taxons.length > 0) {
      chips.push(countChip('taxons', 'sidebar.species', taxons.length, () => this.filterState.clearTaxons()));
    }

    const selectedCategories = this.filterState.selectedCategoryIds();
    if (selectedCategories.length > 0) {
      const typeNameById = this.categoryService.categoryTypeNameById();
      const countPerType = new Map<string, number>();
      let unknownCategories = 0;
      for (const id of selectedCategories) {
        const typeName = typeNameById.get(id);
        if (typeName) {
          countPerType.set(typeName, (countPerType.get(typeName) ?? 0) + 1);
        } else {
          unknownCategories++;
        }
      }
      for (const [typeName, count] of countPerType) {
        chips.push(
          countChip(`categories:${typeName}`, 'sidebar.categoryType.' + typeName, count, () => {
            const byId = this.categoryService.categoryTypeNameById();
            this.filterState.removeCategories(new Set([...byId].filter(([, type]) => type === typeName).map(([id]) => id)));
          }),
        );
      }
      if (unknownCategories > 0) {
        chips.push(
          countChip('categories:other', 'sidebar.categories', unknownCategories, () => {
            const byId = this.categoryService.categoryTypeNameById();
            this.filterState.removeCategories(new Set(this.filterState.selectedCategoryIds().filter((id) => !byId.has(id))));
          }),
        );
      }
    }

    const mainlandCount = this.areaService.mainlandSelectionCount();
    if (mainlandCount > 0) {
      chips.push(countChip('areas:mainland', 'sidebar.areaSection.fastlandsNorge', mainlandCount, () => this.clearMainlandAreas()));
    }
    const svalbardCount = this.areaService.svalbardBjornoyaAndJanMayenSelectionCount();
    if (svalbardCount > 0) {
      chips.push(
        countChip('areas:svalbard', 'sidebar.areaSection.svalbardBjørnøyaAndJanMayen', svalbardCount, () =>
          this.filterState.removeCounties(new Set(this.areaService.svalbardBjornoyaAndJanMayenAreas().map((a) => a.fid))),
        ),
      );
    }
    const oceanAreas = this.filterState.selectedOceanAreaIds();
    if (oceanAreas.length > 0) {
      chips.push(countChip('areas:ocean', 'sidebar.areaSection.oceanAreas', oceanAreas.length, () => this.filterState.clearOceanAreas()));
    }

    const precFrom = this.filterState.coordinatePrecisionFrom();
    const precTo = this.filterState.coordinatePrecisionTo();
    if (precFrom != null || precTo != null) {
      const text = t('sidebar.chipCoordinatePrecision', { from: precFrom ?? 0, to: precTo != null ? String(precTo) : '∞' });
      chips.push({ id: 'coordinatePrecision', text, label: text, clear: () => this.filterState.clearCoordinatePrecision() });
    }

    const periodFrom = this.filterState.periodFrom();
    const periodTo = this.filterState.periodTo();
    if (periodFrom != null || periodTo != null) {
      const text = t('sidebar.chipPeriod', {
        from: periodFrom != null ? String(periodFrom) : '...',
        to: periodTo != null ? String(periodTo) : '...',
      });
      chips.push({ id: 'period', text, label: text, clear: () => this.filterState.clearPeriodYears() });
    }

    const months = this.filterState.selectedMonths();
    if (months.length > 0) {
      chips.push(countChip('months', 'sidebar.periodMonths', months.length, () => this.filterState.clearMonths()));
    }

    const behaviors = this.filterState.selectedBehaviorIds();
    if (behaviors.length > 0) {
      chips.push(countChip('behaviors', 'sidebar.behaviors', behaviors.length, () => this.filterState.clearBehaviors()));
    }

    const basisOfRecords = this.filterState.selectedBasisOfRecordIds();
    if (basisOfRecords.length > 0) {
      chips.push(
        countChip('basisOfRecords', 'sidebar.basisOfRecords', basisOfRecords.length, () => this.filterState.clearBasisOfRecords()),
      );
    }

    const registrationStatusId = this.filterState.selectedRegistrationStatusId();
    if (registrationStatusId !== null) {
      const option = REGISTRATION_STATUS_OPTIONS.find((o) => o.id === registrationStatusId);
      if (option) {
        chips.push(
          namedChip('registrationStatus', 'sidebar.registrering', t(option.labelKey), () => this.filterState.clearRegistrationStatus()),
        );
      }
    }

    const institutions = this.filterState.selectedInstitutionIds();
    if (institutions.length > 0) {
      chips.push(countChip('institutions', 'sidebar.institutions', institutions.length, () => this.filterState.clearInstitutions()));
    }

    const projectName = this.filterState.projectName();
    if (this.filterState.projectOrgId() !== null && projectName) {
      chips.push(
        namedChip('project', 'sidebar.project', projectName, () => {
          this.filterState.setProjectName('');
          this.filterState.setProjectOrgId(null);
        }),
      );
    }

    const datasetName = this.filterState.datasetName();
    if (this.filterState.datasetOrgId() !== null && datasetName) {
      chips.push(
        namedChip('dataset', 'sidebar.dataset', datasetName, () => {
          this.filterState.setDatasetName('');
          this.filterState.setDatasetOrgId(null);
        }),
      );
    }

    const catalogNumber = this.filterState.catalogNumber();
    if (this.filterState.catalogObservationIds().length > 0 && catalogNumber) {
      chips.push(
        namedChip('catalogNumber', 'sidebar.catalogNumber', catalogNumber, () => {
          this.filterState.setCatalogNumber('');
          this.filterState.setCatalogObservationIds([]);
        }),
      );
    }

    const imageFilter = this.filterState.imageFilter();
    if (imageFilter !== 'all') {
      const valueKey = imageFilter === 'withImage' ? 'sidebar.imageWith' : 'sidebar.imageWithout';
      chips.push(namedChip('imageFilter', 'sidebar.image', t(valueKey), () => this.filterState.setImageFilter('all')));
    }

    return chips;
  });

  private clearMainlandAreas(): void {
    const svalbardFids = new Set(this.areaService.svalbardBjornoyaAndJanMayenAreas().map((a) => a.fid));
    this.filterState.removeCounties(new Set(this.filterState.selectedCountyIds().filter((fid) => !svalbardFids.has(fid))));
    this.filterState.clearMunicipalities();
  }
}
