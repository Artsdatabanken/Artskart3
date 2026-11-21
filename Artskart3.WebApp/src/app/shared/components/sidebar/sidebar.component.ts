import { Component, CUSTOM_ELEMENTS_SCHEMA, ElementRef, inject, signal, computed, linkedSignal, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CategoryService, sortCategories } from '../../services/category/category.service';
import { map } from 'rxjs';
import { AreaService, CountyGroup, MAX_HISTORICAL_AREAS, RestrictedArea } from '../../services/area/area.service';
import { InstitutionService } from '../../services/institution/institution.service';
import { BehaviorService, behaviorDisplayName } from '../../services/behavior/behavior.service';
import { BasisOfRecordService, basisOfRecordDisplayName } from '../../services/basis-of-record/basis-of-record.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { AreaSuggestion, withSuggestionDetails } from '../../services/area/area-label';
import {
  BehaviorDto,
  BasisOfRecordDto,
  CadastralUnitDto,
  CategoryTypeDto,
  InstitutionDto,
  TaxonGroupDto,
  OrganizationDto,
  CatalogNumberMatchDto,
  ObservationSearchFilter,
} from '../../types/api.types';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';
import { REGISTRATION_STATUS_OPTIONS } from '@shared/constants/registration-status-options.const';
import { MONTHS } from '@shared/constants/months.const';
import { OrganizationService } from '../../services/organization/organization.service';
import {
  AnnotationFilterOption,
  FilterStateService,
  ImageFilterOption,
  SensitiveFilterOption,
} from '../../services/filter-state/filter-state.service';
import { AuthService } from '../../services/auth/auth.service';
import { SavedFilterService } from '../../services/saved-filter/saved-filter.service';
import { SearchFilterService } from '../../services/search-filter/search-filter.service';
import { FilterChipsComponent, FilterSection } from '../filter-chips/filter-chips.component';
import { SpeciesSearchComponent } from '../species-search/species-search.component';
import { TaxonTreeComponent } from '../taxon-tree/taxon-tree.component';
import { TaxonSelectionService } from '../../services/taxon-selection/taxon-selection.service';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';
import { AutocompleteOptionDirective, AutocompleteSearch } from '../autocomplete/autocomplete-option.directive';
import { HighlightTextComponent } from '../highlight-text/highlight-text.component';
import { CadastralUnitSearchComponent } from '../cadastral-unit-search/cadastral-unit-search.component';
import { CustomAreaControlComponent } from '../custom-area-control/custom-area-control.component';
import { MapViewService } from '../../services/map-view/map-view.service';
import { LoggingService } from '../../logging.service';

// adb-radio-group can't select an empty value, so "Alle" (id null) needs a value of its own.
const ALL_REGISTRATION_STATUSES = 'all';

function toRegistrationStatusValue(id: number | null): string {
  return id === null ? ALL_REGISTRATION_STATUSES : String(id);
}

const SHOW_SCIENTIFIC_NAMES_KEY = 'artskart.taxonTree.showScientificNames';

// Every field must be listed, so a new filter field won't compile until it is decided which section's dot it lights.
const FILTER_SECTION: Record<keyof ObservationSearchFilter, FilterSection | null> = {
  pageNumber: null,
  resultsPerPage: null,
  isPaginated: null,
  taxonGroupIds: 'taxonGroups',
  taxonIds: 'taxonTree',
  includeDescendantTaxa: 'taxonTree',
  categoryIds: 'categories',
  countyIds: 'areas',
  municipalityIds: 'areas',
  oceanAreaIds: 'areas',
  restrictedAreaIds: 'areas',
  cadastralUnitIds: 'areas',
  historicalAreaIds: 'areas',
  customAreaWkt: 'areas',
  coordinatePrecision: 'coordinatePrecision',
  period: 'period',
  behaviorIds: 'behaviors',
  basisOfRecordIds: 'basisOfRecords',
  registrationStatusId: 'basisOfRecords',
  organizationIds: 'institutions',
  projectOrgIds: 'otherProperties',
  datasetOrgIds: 'otherProperties',
  observationIds: 'otherProperties',
  withImages: 'otherProperties',
  changedByAnnotation: 'otherProperties',
  hiddenObservations: 'otherProperties',
  sensitive: 'otherProperties',
  // No control in the sidebar; only saved filters set these.
  uncertainIdentification: null,
  introduced: null,
};

type SelectAllState = 'none' | 'some' | 'all';

// A Lit element: updateComplete settles once a property change has been rendered.
type AccordionItemElement = HTMLElement & { open: boolean; updateComplete?: Promise<boolean> };

function listIds(items: readonly { id?: number }[] | null | undefined): number[] {
  return (items ?? []).flatMap((item) => (item.id == null ? [] : [item.id]));
}

function selectAllState(ids: readonly number[], selectedIds: readonly number[]): SelectAllState {
  const selected = new Set(selectedIds);
  const selectedCount = ids.filter((id) => selected.has(id)).length;
  if (selectedCount === 0) return 'none';
  return selectedCount === ids.length ? 'all' : 'some';
}

@Component({
  selector: 'app-sidebar',
  imports: [
    TranslateModule,
    FormatNumberPipe,
    FilterChipsComponent,
    SpeciesSearchComponent,
    TaxonTreeComponent,
    RiskCategoryBadgeComponent,
    AutocompleteComponent,
    AutocompleteOptionDirective,
    HighlightTextComponent,
    CadastralUnitSearchComponent,
    CustomAreaControlComponent,
  ],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
})
export class SidebarComponent {
  private readonly categoryService = inject(CategoryService);
  private readonly areaService = inject(AreaService);
  private readonly institutionService = inject(InstitutionService);
  private readonly behaviorService = inject(BehaviorService);
  private readonly basisOfRecordService = inject(BasisOfRecordService);
  private readonly taxonGroupService = inject(TaxonGroupService);
  private readonly organizationService = inject(OrganizationService);
  private readonly filterState = inject(FilterStateService);
  private readonly searchFilter = inject(SearchFilterService);
  private readonly savedFilterService = inject(SavedFilterService);
  private readonly mapView = inject(MapViewService);
  protected readonly authService = inject(AuthService);
  private readonly logger = inject(LoggingService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly taxonSelection = inject(TaxonSelectionService);

  readonly saveFilterRequested = output<void>();

  readonly showUseDefaultFilter = computed(
    () => this.authService.isAuthenticated() && !this.searchFilter.hasActiveFilter() && this.savedFilterService.defaultFilter() !== null,
  );
  protected readonly translate = inject(TranslateService);

  /**
   * Sections whose filter differs from Artskart's defaults; each one gets a dot on its accordion heading.
   * `observationFilter` leaves every field at its default `undefined`, so a defined field is a selection.
   */
  readonly sectionsWithSelection = computed(() => {
    const filter = this.searchFilter.observationFilter();
    return new Set(
      (Object.keys(filter) as (keyof ObservationSearchFilter)[]).flatMap((key) => {
        const section = FILTER_SECTION[key];
        return section && filter[key] !== undefined ? [section] : [];
      }),
    );
  });

  readonly registreringOptions = REGISTRATION_STATUS_OPTIONS.map((option) => ({
    ...option,
    value: toRegistrationStatusValue(option.id),
  }));
  readonly registrationStatusValue = computed(() => toRegistrationStatusValue(this.filterState.selectedRegistrationStatusId()));
  readonly selectedCadastralUnitIds = this.filterState.selectedCadastralUnitIds;

  readonly categoriesResource = rxResource<CategoryTypeDto[], void>({
    stream: () => this.categoryService.getCategories(),
  });

  readonly institutionsResource = rxResource<InstitutionDto[], void>({
    stream: () => this.institutionService.getInstitutions(),
  });

  readonly behaviorsResource = rxResource<BehaviorDto[], void>({
    stream: () => this.behaviorService.getBehaviors(),
  });

  readonly basisOfRecordsResource = rxResource<BasisOfRecordDto[], void>({
    stream: () => this.basisOfRecordService.getBasisOfRecords(),
  });

  readonly taxonGroupsResource = rxResource<TaxonGroupDto[], void>({
    stream: () => this.taxonGroupService.getTaxonGroups(),
  });

  readonly categoryTypes = this.categoriesResource.value;
  readonly institutions = this.institutionsResource.value;
  readonly behaviors = this.behaviorsResource.value;
  readonly basisOfRecords = computed(() => {
    const selected = this.filterState.selectedBasisOfRecordIds();
    const list = (this.basisOfRecordsResource.value() ?? []).filter(
      (b) => (b.observationCount ?? 0) > 0 || (b.id !== undefined && b.id !== null && selected.includes(b.id)),
    );
    return [...list].sort((a, b) => {
      const nameA = this.getBasisOfRecordDisplayName(a).toLowerCase();
      const nameB = this.getBasisOfRecordDisplayName(b).toLowerCase();
      return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
    });
  });
  readonly taxonGroups = this.taxonGroupsResource.value;
  readonly countyGroups = this.areaService.countyGroups;
  readonly svalbardBjornoyaAndJanMayenAreas = this.areaService.svalbardBjornoyaAndJanMayenAreas;
  readonly oceanAreaGroup = this.areaService.oceanAreaGroup;

  private readonly taxonGroupIds = computed(() => listIds(this.taxonGroups()));
  private readonly behaviorIds = computed(() => listIds(this.behaviors()));
  private readonly basisOfRecordIds = computed(() => listIds(this.basisOfRecords()));

  readonly taxonGroupsSelectAll = computed(() => selectAllState(this.taxonGroupIds(), this.filterState.selectedTaxonGroupIds()));
  readonly behaviorsSelectAll = computed(() => selectAllState(this.behaviorIds(), this.filterState.selectedBehaviorIds()));
  readonly basisOfRecordsSelectAll = computed(() =>
    selectAllState(this.basisOfRecordIds(), this.filterState.selectedBasisOfRecordIds()),
  );
  // Keyed by the type objects the template iterates over; each category type has its own "select all".
  readonly categoryTypesSelectAll = computed(() => {
    const selected = this.filterState.selectedCategoryIds();
    return new Map((this.categoryTypes() ?? []).map((type) => [type, selectAllState(listIds(type.categories), selected)]));
  });

  onSelectAllTaxonGroups(): void {
    if (this.taxonGroupsSelectAll() === 'all') this.filterState.clearTaxonGroups();
    else this.filterState.addTaxonGroups(this.taxonGroupIds());
  }

  onSelectAllBehaviors(): void {
    if (this.behaviorsSelectAll() === 'all') this.filterState.clearBehaviors();
    else this.filterState.addBehaviors(this.behaviorIds());
  }

  onSelectAllBasisOfRecords(): void {
    if (this.basisOfRecordsSelectAll() === 'all') this.filterState.clearBasisOfRecords();
    else this.filterState.addBasisOfRecords(this.basisOfRecordIds());
  }

  onSelectAllCategories(type: CategoryTypeDto): void {
    const ids = listIds(type.categories);
    if (this.categoryTypesSelectAll().get(type) === 'all') this.filterState.removeCategories(new Set(ids));
    else this.filterState.addCategories(ids);
  }

  isCategorySelected(id: number): boolean {
    return this.filterState.selectedCategoryIds().includes(id);
  }

  onCategoryToggle(id: number): void {
    this.filterState.toggleCategory(id);
  }

  readonly sortCategories = sortCategories;

  onClearFilter(): void {
    this.filterState.clearAll();
  }

  onUseDefaultFilter(): void {
    const defaultFilter = this.savedFilterService.defaultFilter();
    if (defaultFilter) this.savedFilterService.activate(defaultFilter);
  }

  isMunicipalitySelected(fid: string): boolean {
    return this.filterState.selectedMunicipalityIds().includes(fid);
  }

  isOceanAreaSelected(fid: string): boolean {
    return this.filterState.selectedOceanAreaIds().includes(fid);
  }

  onOceanAreaToggle(fid: string): void {
    this.filterState.toggleOceanArea(fid);
  }

  isCountySelected(fid: string): boolean {
    return this.filterState.selectedCountyIds().includes(fid);
  }

  onCountyCheckboxToggle(fid: string): void {
    this.filterState.toggleCounty(fid);
  }

  isAllInCountySelected(group: CountyGroup): boolean {
    const municipalityFids = group.municipalities.map((m) => m.fid!);
    if (municipalityFids.length === 0) return false;
    const selected = this.filterState.selectedMunicipalityIds();
    return municipalityFids.every((fid) => selected.includes(fid));
  }

  isSomeInCountySelected(group: CountyGroup): boolean {
    const municipalityFids = group.municipalities.map((m) => m.fid!);
    const selected = this.filterState.selectedMunicipalityIds();
    const count = municipalityFids.filter((fid) => selected.includes(fid)).length;
    return count > 0 && count < municipalityFids.length;
  }

  onMunicipalityToggle(fid: string): void {
    this.filterState.toggleMunicipality(fid);
  }

  onCountyToggle(group: CountyGroup): void {
    const municipalityFids = group.municipalities.map((m) => m.fid!);
    if (this.isAllInCountySelected(group)) {
      municipalityFids.forEach((fid) => this.filterState.removeMunicipality(fid));
    } else {
      municipalityFids.forEach((fid) => this.filterState.addMunicipality(fid));
    }
  }

  isInstitutionSelected(id: number): boolean {
    return this.filterState.selectedInstitutionIds().includes(id);
  }

  onInstitutionToggle(id: number): void {
    this.filterState.toggleInstitution(id);
  }

  isBehaviorSelected(id: number): boolean {
    return this.filterState.selectedBehaviorIds().includes(id);
  }

  onBehaviorToggle(id: number): void {
    this.filterState.toggleBehavior(id);
  }

  getBehaviorDisplayName(behavior: BehaviorDto): string {
    return behaviorDisplayName(behavior, (key) => this.translate.instant(key));
  }

  isBasisOfRecordSelected(id: number): boolean {
    return this.filterState.selectedBasisOfRecordIds().includes(id);
  }

  onBasisOfRecordToggle(id: number): void {
    this.filterState.toggleBasisOfRecord(id);
  }

  onRegistrationStatusChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.filterState.setRegistrationStatus(target.value === ALL_REGISTRATION_STATUSES ? null : Number(target.value));
    }
  }

  getBasisOfRecordDisplayName(basisOfRecord: BasisOfRecordDto): string {
    return basisOfRecordDisplayName(basisOfRecord, (key) => this.translate.instant(key));
  }

  isTaxonGroupSelected(id: number): boolean {
    return this.filterState.selectedTaxonGroupIds().includes(id);
  }

  onTaxonGroupToggle(id: number): void {
    this.filterState.toggleTaxonGroup(id);
  }

  // Taxon tree lazy load
  readonly taxonTreeOpened = signal(false);
  readonly taxonTreeReveal = signal(0);

  onTaxonTreeToggle(): void {
    this.taxonTreeOpened.set(true);
  }

  /** Opens the section where a chip's filter is set, with the selections in it unfolded. */
  async onOpenFilter(section: FilterSection): Promise<void> {
    const item = this.host.nativeElement.querySelector<AccordionItemElement>(`adb-accordion-item[data-filter-section="${section}"]`);
    if (!item) return;
    item.open = true;
    item.querySelectorAll<AccordionItemElement>('adb-accordion-item[data-partly-selected]').forEach((county) => (county.open = true));
    // Opening an item from code fires no toggle event.
    if (section === 'taxonTree') this.taxonTreeOpened.set(true);

    // Until the section has rendered open, there may not be room below it to scroll it to the top.
    await item.updateComplete;
    item.scrollIntoView({ block: 'start' });
    // The chip is out of view now, so focus moves on to the section's header.
    item.shadowRoot?.querySelector<HTMLElement>('summary')?.focus({ preventScroll: true });

    if (section === 'taxonTree') {
      // The tree finds the selected taxa by their ancestries, which may still be loading.
      await this.taxonSelection.ancestriesResolved();
      this.taxonTreeReveal.update((n) => n + 1);
    }
  }

  readonly includeDescendantTaxa = this.filterState.includeDescendantTaxa;

  onIncludeDescendantTaxaChange(event: Event): void {
    this.filterState.setIncludeDescendantTaxa((event.target as HTMLInputElement).checked);
  }

  readonly showScientificNames = signal(this.readShowScientificNames());

  onShowScientificNamesChange(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.showScientificNames.set(checked);
    try {
      localStorage.setItem(SHOW_SCIENTIFIC_NAMES_KEY, String(checked));
    } catch (e: unknown) {
      this.logger.warn('Failed to save scientific names preference to localStorage:', 'SidebarComponent', e);
    }
  }

  private readShowScientificNames(): boolean {
    try {
      return localStorage.getItem(SHOW_SCIENTIFIC_NAMES_KEY) === 'true';
    } catch (e: unknown) {
      this.logger.warn('Failed to retrieve scientific names preference from localStorage:', 'SidebarComponent', e);
      return false;
    }
  }

  readonly coordinatePrecisionFromInput = linkedSignal(() => {
    const value = this.filterState.coordinatePrecisionFrom();
    return value == null ? '' : String(value);
  });
  readonly coordinatePrecisionToInput = linkedSignal(() => {
    const value = this.filterState.coordinatePrecisionTo();
    return value == null ? '' : String(value);
  });

  onCoordinatePrecisionFromChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const filtered = input.value.replace(/\D/g, '');
    input.value = filtered;
    this.coordinatePrecisionFromInput.set(filtered);
  }

  onCoordinatePrecisionToChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const filtered = input.value.replace(/\D/g, '');
    input.value = filtered;
    this.coordinatePrecisionToInput.set(filtered);
  }

  onApplyCoordinatePrecision(): void {
    const fromStr = this.coordinatePrecisionFromInput().trim();
    const toStr = this.coordinatePrecisionToInput().trim();

    let from = fromStr === '' ? null : Number(fromStr);
    let to = toStr === '' ? null : Number(toStr);

    if (fromStr !== '' && (!Number.isInteger(from) || from! < 0)) return;
    if (toStr !== '' && (!Number.isInteger(to) || to! < 0)) return;

    if (from != null && to != null && from > to) {
      [from, to] = [to, from];
      this.coordinatePrecisionFromInput.set(String(from));
      this.coordinatePrecisionToInput.set(String(to));
    }

    this.filterState.setCoordinatePrecision(from, to);
  }

  // Period filter
  readonly periodFromInput = linkedSignal(() => {
    const value = this.filterState.periodFrom();
    return value == null ? '' : String(value);
  });
  readonly periodToInput = linkedSignal(() => {
    const value = this.filterState.periodTo();
    return value == null ? '' : String(value);
  });

  onPeriodFromChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const filtered = input.value.replace(/\D/g, '').slice(0, 4);
    input.value = filtered;
    this.periodFromInput.set(filtered);
  }

  onPeriodToChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const filtered = input.value.replace(/\D/g, '').slice(0, 4);
    input.value = filtered;
    this.periodToInput.set(filtered);
  }

  onApplyPeriod(): void {
    const fromStr = this.periodFromInput().trim();
    const toStr = this.periodToInput().trim();

    let from = fromStr === '' ? null : Number(fromStr);
    let to = toStr === '' ? null : Number(toStr);

    if (fromStr !== '' && (!Number.isInteger(from) || from! < 0)) return;
    if (toStr !== '' && (!Number.isInteger(to) || to! < 0)) return;

    if (from != null && to != null && from > to) {
      [from, to] = [to, from];
      this.periodFromInput.set(String(from));
      this.periodToInput.set(String(to));
    }

    this.filterState.setPeriod(from, to);
  }

  readonly imageFilter = this.filterState.imageFilter;
  readonly annotationFilter = this.filterState.annotationFilter;
  readonly sensitiveFilter = this.filterState.sensitiveFilter;

  // Free text is matched by the Lookup endpoints; the filter only ever gets ids.
  readonly searchProjects: AutocompleteSearch<OrganizationDto> = (term) => this.organizationService.searchProjects(term);
  readonly searchDatasets: AutocompleteSearch<OrganizationDto> = (term) => this.organizationService.searchDatasets(term);
  readonly searchCatalogNumbers: AutocompleteSearch<CatalogNumberMatchDto> = (term) => this.organizationService.searchCatalogNumbers(term);
  // Same as the API's MinCatalogNumberSearchLength; shorter terms always come back empty.
  readonly catalogNumberMinLength = 2;
  readonly trackByOrganizationId = (organization: OrganizationDto) => organization.id;
  readonly trackByCatalogNumber = (match: CatalogNumberMatchDto) => match.catalogNumber;

  readonly searchRestrictedAreas: AutocompleteSearch<RestrictedArea> = (term) => this.areaService.searchRestrictedAreas(term);
  readonly trackByFid = (area: RestrictedArea) => area.fid;

  onRestrictedAreaSelected(area: RestrictedArea): void {
    this.filterState.addRestrictedArea(area.fid);
  }

  readonly searchAreas: AutocompleteSearch<AreaSuggestion> = (term) =>
    this.areaService
      .searchAreas(term)
      .pipe(map((areas) => withSuggestionDetails(areas, (key, params) => this.translate.instant(key, params))));
  readonly trackByAreaId = (suggestion: AreaSuggestion) => suggestion.area.id;

  readonly maxHistoricalAreas = MAX_HISTORICAL_AREAS;
  private readonly historicalAreaRefused = signal(false);
  /** Replaces the hint after a historical area was refused, until one is removed. */
  readonly historicalAreaLimitReached = computed(
    () => this.historicalAreaRefused() && this.filterState.selectedHistoricalAreaIds().length >= MAX_HISTORICAL_AREAS,
  );

  onAreaSelected({ area }: AreaSuggestion): void {
    const selected = this.filterState.selectedHistoricalAreaIds();
    const refused = !area.isCurrent && selected.length >= MAX_HISTORICAL_AREAS && !selected.includes(area.id!);
    this.historicalAreaRefused.set(refused);
    if (!refused) this.areaService.addSearchedArea(area);
  }

  onProjectSelected(organization: OrganizationDto): void {
    if (organization.id == null) return;
    this.filterState.addProject({ id: organization.id, name: organization.name ?? '' });
  }

  onCadastralUnitSelected(cadastralUnit: CadastralUnitDto): void {
    if (!cadastralUnit.id) return;
    this.filterState.addCadastralUnit(cadastralUnit.id);

    const { minX, minY, maxX, maxY } = cadastralUnit.extent ?? {};
    if (minX == null || minY == null || maxX == null || maxY == null) return;
    const margin = Math.max(maxX - minX, maxY - minY) * 0.25;
    this.mapView.requestFit([minX - margin, minY - margin, maxX + margin, maxY + margin]);
  }

  onDatasetSelected(organization: OrganizationDto): void {
    if (organization.id == null) return;
    this.filterState.addDataset({ id: organization.id, name: organization.name ?? '' });
  }

  // Treffet bærer ObservationId-ene med seg, så det trengs ikke noe ekstra kall
  // for å gjøre om katalognummeret til et filter.
  onCatalogNumberSelected(match: CatalogNumberMatchDto): void {
    this.filterState.setCatalogNumber(match.catalogNumber ?? '');
    this.filterState.setCatalogObservationIds(match.observationIds ?? []);
  }

  onImageFilterChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.filterState.setImageFilter(target.value as ImageFilterOption);
    }
  }

  onAnnotationFilterChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.filterState.setAnnotationFilter(target.value as AnnotationFilterOption);
    }
  }

  onSensitiveFilterChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.filterState.setSensitiveFilter(target.value as SensitiveFilterOption);
    }
  }

  readonly months = MONTHS;

  isMonthSelected(month: number): boolean {
    return this.filterState.selectedMonths().includes(month);
  }

  onMonthToggle(month: number): void {
    this.filterState.toggleMonth(month);
  }
}
