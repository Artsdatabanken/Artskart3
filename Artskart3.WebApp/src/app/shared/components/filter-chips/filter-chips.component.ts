import { Component, CUSTOM_ELEMENTS_SCHEMA, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, of } from 'rxjs';
import { DEFAULT_ANNOTATION_FILTER, FilterStateService } from '../../services/filter-state/filter-state.service';
import { AreaService } from '../../services/area/area.service';
import { areaLabel } from '../../services/area/area-label';
import { CategoryService, sortCategories } from '../../services/category/category.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { BehaviorService, behaviorDisplayName } from '../../services/behavior/behavior.service';
import { BasisOfRecordService, basisOfRecordDisplayName } from '../../services/basis-of-record/basis-of-record.service';
import { InstitutionService } from '../../services/institution/institution.service';
import { TaxonSelectionService, taxonDisplayName } from '../../services/taxon-selection/taxon-selection.service';
import { REGISTRATION_STATUS_OPTIONS } from '@shared/constants/registration-status-options.const';
import { MONTHS } from '@shared/constants/months.const';
import { TooltipComponent } from '../tooltip/tooltip.component';

/** The accordion section in the filter panel where a filter is set. */
export type FilterSection =
  | 'taxonGroups'
  | 'taxonTree'
  | 'categories'
  | 'areas'
  | 'coordinatePrecision'
  | 'period'
  | 'behaviors'
  | 'basisOfRecords'
  | 'institutions'
  | 'otherProperties';

export interface FilterChip {
  id: string;
  label: string;
  text: string;
  suffix?: string;
  /** The tooltip: what is selected, without implicit selections such as the municipalities of a whole county. */
  details: string;
  section: FilterSection;
  clear: () => void;
}

// Keeps the tooltip of a long selection within the screen.
const MaxNamesInDetails = 20;

function lookupList<T>(source: Observable<T[]>) {
  return toSignal(source.pipe(catchError(() => of([]))), { initialValue: [] as T[] });
}

/** Scientific names mark their italic parts with `<i>`, which the taxon tree renders but a tooltip can't. */
function withoutMarkup(name: string): string {
  return name.replace(/<\/?i>/g, '');
}

/** The names of the selected items in list order, then the ids that aren't in the list (yet). */
function selectedNames<T extends { id?: number | null }>(items: readonly T[], selectedIds: readonly number[], name: (item: T) => string) {
  const selected = new Set(selectedIds);
  const listed = new Set(items.map((item) => item.id));
  return [
    ...items.filter((item) => item.id != null && selected.has(item.id)).map(name),
    ...selectedIds.filter((id) => !listed.has(id)).map((id) => `#${id}`),
  ];
}

@Component({
  selector: 'app-filter-chips',
  imports: [TooltipComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './filter-chips.component.html',
  styleUrl: './filter-chips.component.css',
})
export class FilterChipsComponent {
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly categoryService = inject(CategoryService);
  private readonly taxonSelection = inject(TaxonSelectionService);
  private readonly translate = inject(TranslateService);
  private readonly currentLang = signal(this.translate.getCurrentLang() || this.translate.getCurrentLang());

  private readonly taxonGroups = lookupList(inject(TaxonGroupService).getTaxonGroups());
  private readonly behaviors = lookupList(inject(BehaviorService).getBehaviors());
  private readonly basisOfRecords = lookupList(inject(BasisOfRecordService).getBasisOfRecords());
  private readonly institutions = lookupList(inject(InstitutionService).getInstitutions());

  readonly showScientificNames = input(false);
  /** A chip was clicked, to show where its filter is set. */
  readonly openFilter = output<FilterSection>();

  constructor() {
    this.translate.onLangChange.pipe(takeUntilDestroyed()).subscribe((event) => {
      this.currentLang.set(event.lang);
    });
  }

  readonly chips = computed((): FilterChip[] => {
    this.currentLang();
    const t = (key: string, params?: Record<string, unknown>) => this.translate.instant(key, params);
    const chips: FilterChip[] = [];

    const listNames = (names: string[]) => {
      if (names.length <= MaxNamesInDetails) return names.join(', ');
      const listed = names.slice(0, MaxNamesInDetails).join(', ');
      return `${listed} ${t('sidebar.chipMoreNames', { count: names.length - MaxNamesInDetails })}`;
    };

    const countChip = (id: string, titleKey: string, names: string[], section: FilterSection, clear: () => void): FilterChip => {
      const text = t(titleKey);
      const suffix = `(${names.length})`;
      return { id, text, suffix, label: `${text} ${suffix}`, details: listNames(names), section, clear };
    };

    const textChip = (id: string, text: string, section: FilterSection, clear: () => void): FilterChip => ({
      id,
      text,
      label: text,
      details: text,
      section,
      clear,
    });

    const namedChip = (id: string, titleKey: string, value: string, section: FilterSection, clear: () => void): FilterChip =>
      textChip(id, `${t(titleKey)}: ${value}`, section, clear);

    const taxonGroupIds = this.filterState.selectedTaxonGroupIds();
    if (taxonGroupIds.length > 0) {
      const names = selectedNames(this.taxonGroups(), taxonGroupIds, (group) => group.name ?? `#${group.id}`);
      chips.push(countChip('taxonGroups', 'sidebar.taxonGroups', names, 'taxonGroups', () => this.filterState.clearTaxonGroups()));
    }

    const taxonIds = this.filterState.selectedTaxonIds();
    if (taxonIds.length > 0) {
      // Until a taxon's names have been looked up, the tooltip shows its id.
      const names = taxonIds.map((id) => {
        const taxonNames = this.taxonSelection.names(id);
        const name = taxonNames && taxonDisplayName(taxonNames, this.showScientificNames());
        return name ? withoutMarkup(name) : `#${id}`;
      });
      chips.push(countChip('taxons', 'sidebar.species', names, 'taxonTree', () => this.filterState.clearTaxons()));
    }

    if (this.filterState.excludesDescendantTaxa()) {
      chips.push(
        textChip('includeDescendantTaxa', t('sidebar.excludesDescendantTaxa'), 'taxonTree', () =>
          this.filterState.setIncludeDescendantTaxa(true),
        ),
      );
    }

    const selectedCategories = new Set(this.filterState.selectedCategoryIds());
    if (selectedCategories.size > 0) {
      const namedCategories = new Set<number>();
      for (const type of this.categoryService.categoryTypes()) {
        const typeName = type.name;
        if (!typeName) continue;
        const selected = sortCategories(type.categories).filter((c) => c.id != null && selectedCategories.has(c.id));
        if (selected.length === 0) continue;
        selected.forEach((c) => namedCategories.add(c.id!));
        chips.push(
          countChip(
            `categories:${typeName}`,
            'sidebar.categoryType.' + typeName,
            selected.map((c) => c.name ?? `#${c.id}`),
            'categories',
            () => {
              const byId = this.categoryService.categoryTypeNameById();
              this.filterState.removeCategories(new Set([...byId].filter(([, type]) => type === typeName).map(([id]) => id)));
            },
          ),
        );
      }
      const unknownCategories = [...selectedCategories].filter((id) => !namedCategories.has(id));
      if (unknownCategories.length > 0) {
        chips.push(
          countChip(
            'categories:other',
            'sidebar.categories',
            unknownCategories.map((id) => `#${id}`),
            'categories',
            () => {
              const byId = this.categoryService.categoryTypeNameById();
              this.filterState.removeCategories(new Set(this.filterState.selectedCategoryIds().filter((id) => !byId.has(id))));
            },
          ),
        );
      }
    }

    const mainlandNames = this.areaService.mainlandSelectionNames();
    if (mainlandNames.length > 0) {
      chips.push(
        countChip('areas:mainland', 'sidebar.areaSection.fastlandsNorge', mainlandNames, 'areas', () => this.clearMainlandAreas()),
      );
    }
    const svalbardNames = this.areaService.svalbardBjornoyaAndJanMayenSelectionNames();
    if (svalbardNames.length > 0) {
      chips.push(
        countChip('areas:svalbard', 'sidebar.areaSection.svalbardBjørnøyaAndJanMayen', svalbardNames, 'areas', () =>
          this.filterState.removeCounties(new Set(this.areaService.svalbardBjornoyaAndJanMayenAreas().map((a) => a.fid))),
        ),
      );
    }
    const oceanNames = this.areaService.oceanAreaSelectionNames();
    if (oceanNames.length > 0) {
      chips.push(countChip('areas:ocean', 'sidebar.areaSection.oceanAreas', oceanNames, 'areas', () => this.filterState.clearOceanAreas()));
    }
    // Until the area list has loaded, the chip shows the fid.
    for (const fid of this.filterState.selectedRestrictedAreaIds()) {
      const name = this.areaService.restrictedAreaName(fid) ?? fid;
      chips.push(
        namedChip(`restrictedArea:${fid}`, 'sidebar.restrictedArea', name, 'areas', () => this.filterState.removeRestrictedArea(fid)),
      );
    }
    for (const id of this.filterState.selectedCadastralUnitIds()) {
      chips.push(namedChip(`cadastralUnit:${id}`, 'sidebar.cadastralUnit', id, 'areas', () => this.filterState.removeCadastralUnit(id)));
    }
    // Until a saved filter's areas have been looked up, the chip shows the id.
    for (const area of this.filterState.selectedHistoricalAreas()) {
      const id = area.id!;
      const text = area.name ? areaLabel(area, t) : `#${id}`;
      chips.push(namedChip(`historicalArea:${id}`, 'sidebar.areaSearch', text, 'areas', () => this.filterState.removeHistoricalArea(id)));
    }
    if (this.filterState.customAreaWkt()) {
      chips.push(textChip('customArea', t('sidebar.customArea.heading'), 'areas', () => this.filterState.clearCustomArea()));
    }

    const precFrom = this.filterState.coordinatePrecisionFrom();
    const precTo = this.filterState.coordinatePrecisionTo();
    if (precFrom != null || precTo != null) {
      const text = t('sidebar.chipCoordinatePrecision', { from: precFrom ?? 0, to: precTo != null ? String(precTo) : '∞' });
      chips.push(textChip('coordinatePrecision', text, 'coordinatePrecision', () => this.filterState.clearCoordinatePrecision()));
    }

    const periodFrom = this.filterState.periodFrom();
    const periodTo = this.filterState.periodTo();
    if (periodFrom != null || periodTo != null) {
      const text = t('sidebar.chipPeriod', {
        from: periodFrom != null ? String(periodFrom) : '...',
        to: periodTo != null ? String(periodTo) : '...',
      });
      chips.push(textChip('period', text, 'period', () => this.filterState.clearPeriodYears()));
    }

    const months = this.filterState.selectedMonths();
    if (months.length > 0) {
      const labelKeys = new Map<number, string>(MONTHS.map((month) => [month.value, month.labelKey]));
      const names = [...months]
        .sort((a, b) => a - b)
        .map((month) => {
          const labelKey = labelKeys.get(month);
          return labelKey ? t(labelKey) : `#${month}`;
        });
      chips.push(countChip('months', 'sidebar.periodMonths', names, 'period', () => this.filterState.clearMonths()));
    }

    const behaviorIds = this.filterState.selectedBehaviorIds();
    if (behaviorIds.length > 0) {
      const names = selectedNames(this.behaviors(), behaviorIds, (behavior) => behaviorDisplayName(behavior, t));
      chips.push(countChip('behaviors', 'sidebar.behaviors', names, 'behaviors', () => this.filterState.clearBehaviors()));
    }

    const basisOfRecordIds = this.filterState.selectedBasisOfRecordIds();
    if (basisOfRecordIds.length > 0) {
      const names = selectedNames(this.basisOfRecords(), basisOfRecordIds, (basisOfRecord) => basisOfRecordDisplayName(basisOfRecord, t));
      chips.push(
        countChip('basisOfRecords', 'sidebar.basisOfRecords', names, 'basisOfRecords', () => this.filterState.clearBasisOfRecords()),
      );
    }

    const registrationStatusId = this.filterState.selectedRegistrationStatusId();
    if (registrationStatusId !== null) {
      const option = REGISTRATION_STATUS_OPTIONS.find((o) => o.id === registrationStatusId);
      if (option) {
        chips.push(
          namedChip('registrationStatus', 'sidebar.registrering', t(option.labelKey), 'basisOfRecords', () =>
            this.filterState.clearRegistrationStatus(),
          ),
        );
      }
    }

    const institutionIds = this.filterState.selectedInstitutionIds();
    if (institutionIds.length > 0) {
      const names = selectedNames(this.institutions(), institutionIds, (institution) => institution.name ?? `#${institution.id}`);
      chips.push(countChip('institutions', 'sidebar.institutions', names, 'institutions', () => this.filterState.clearInstitutions()));
    }

    // Until a saved filter's names have been looked up, the chip shows the id.
    for (const project of this.filterState.selectedProjects()) {
      chips.push(
        namedChip(`project:${project.id}`, 'sidebar.project', project.name || `#${project.id}`, 'otherProperties', () =>
          this.filterState.removeProject(project.id),
        ),
      );
    }

    for (const dataset of this.filterState.selectedDatasets()) {
      chips.push(
        namedChip(`dataset:${dataset.id}`, 'sidebar.dataset', dataset.name || `#${dataset.id}`, 'otherProperties', () =>
          this.filterState.removeDataset(dataset.id),
        ),
      );
    }

    const catalogNumber = this.filterState.catalogNumber();
    if (this.filterState.catalogObservationIds().length > 0 && catalogNumber) {
      chips.push(
        namedChip('catalogNumber', 'sidebar.catalogNumber', catalogNumber, 'otherProperties', () => {
          this.filterState.setCatalogNumber('');
          this.filterState.setCatalogObservationIds([]);
        }),
      );
    }

    const imageFilter = this.filterState.imageFilter();
    if (imageFilter !== 'all') {
      const valueKey = imageFilter === 'withImage' ? 'sidebar.imageWith' : 'sidebar.imageWithout';
      chips.push(namedChip('imageFilter', 'sidebar.image', t(valueKey), 'otherProperties', () => this.filterState.setImageFilter('all')));
    }

    const sensitiveFilter = this.filterState.sensitiveFilter();
    if (sensitiveFilter !== 'all') {
      const valueKey = sensitiveFilter === 'sensitive' ? 'sidebar.sensitiveOnly' : 'sidebar.sensitiveNot';
      chips.push(
        namedChip('sensitiveFilter', 'sidebar.sensitivity', t(valueKey), 'otherProperties', () =>
          this.filterState.setSensitiveFilter('all'),
        ),
      );
    }

    const annotationFilter = this.filterState.annotationFilter();
    if (annotationFilter !== DEFAULT_ANNOTATION_FILTER) {
      const valueKey =
        annotationFilter === 'all'
          ? 'sidebar.annotationAll'
          : annotationFilter === 'hidden'
            ? 'sidebar.annotationHidden'
            : 'sidebar.annotationAnnotated';
      chips.push(
        namedChip('annotationFilter', 'sidebar.annotation', t(valueKey), 'otherProperties', () =>
          this.filterState.setAnnotationFilter(DEFAULT_ANNOTATION_FILTER),
        ),
      );
    }

    return chips;
  });

  private clearMainlandAreas(): void {
    const svalbardFids = new Set(this.areaService.svalbardBjornoyaAndJanMayenAreas().map((a) => a.fid));
    this.filterState.removeCounties(new Set(this.filterState.selectedCountyIds().filter((fid) => !svalbardFids.has(fid))));
    this.filterState.clearMunicipalities();
  }
}
