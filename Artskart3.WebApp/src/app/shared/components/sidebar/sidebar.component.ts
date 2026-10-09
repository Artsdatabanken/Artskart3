import { Component, CUSTOM_ELEMENTS_SCHEMA, inject, signal, computed, linkedSignal, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CategoryService } from '../../services/category/category.service';
import { AreaService, CountyGroup } from '../../services/area/area.service';
import { InstitutionService } from '../../services/institution/institution.service';
import { BehaviorService } from '../../services/behavior/behavior.service';
import { BasisOfRecordService } from '../../services/basis-of-record/basis-of-record.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { BehaviorDto, BasisOfRecordDto, CategoryTypeDto, InstitutionDto, TaxonGroupDto, CategoryDto } from '../../types/api.types';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';
import { CATEGORY_ORDER } from '@shared/constants/category-order.const';
import { REGISTRATION_STATUS_OPTIONS } from '@shared/constants/registration-status-options.const';
import { OrganizationService } from '../../services/organization/organization.service';
import { FilterStateService, ImageFilterOption } from '../../services/filter-state/filter-state.service';
import { AuthService } from '../../services/auth/auth.service';
import { SavedFilterService } from '../../services/saved-filter/saved-filter.service';
import { SearchFilterService } from '../../services/search-filter/search-filter.service';
import { FilterChipsComponent } from '../filter-chips/filter-chips.component';
import { SpeciesSearchComponent } from '../species-search/species-search.component';
import { TaxonTreeComponent } from '../taxon-tree/taxon-tree.component';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';
import { AutocompleteOptionDirective, AutocompleteSearch } from '../autocomplete/autocomplete-option.directive';
import { HighlightTextComponent } from '../highlight-text/highlight-text.component';
import type { components } from '../../types/api.generated';

type OrganizationDto = components['schemas']['OrganizationDto'];
type CatalogNumberMatchDto = components['schemas']['CatalogNumberMatchDto'];

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
  protected readonly authService = inject(AuthService);

  readonly saveFilterRequested = output<void>();

  readonly showUseDefaultFilter = computed(
    () => this.authService.isAuthenticated() && !this.searchFilter.hasActiveFilter() && this.savedFilterService.defaultFilter() !== null,
  );
  protected readonly translate = inject(TranslateService);

  readonly registreringOptions = REGISTRATION_STATUS_OPTIONS;

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
    const list = this.basisOfRecordsResource.value() ?? [];
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

  isCategorySelected(id: number): boolean {
    return this.filterState.selectedCategoryIds().includes(id);
  }

  onCategoryToggle(id: number): void {
    this.filterState.toggleCategory(id);
  }

  getSortedCategories(categories: CategoryDto[] | null | undefined): CategoryDto[] {
    if (!categories) return [];
    return [...categories].sort((a, b) => {
      const indexA = a.code ? CATEGORY_ORDER.indexOf(a.code) : -1;
      const indexB = b.code ? CATEGORY_ORDER.indexOf(b.code) : -1;
      return (indexA === -1 ? Infinity : indexA) - (indexB === -1 ? Infinity : indexB);
    });
  }

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
    if (!behavior.name) return behavior.description ?? '';
    const key = 'sidebar.behaviorName.' + behavior.name;
    const translated = this.translate.instant(key);
    return translated !== key ? translated : (behavior.description ?? behavior.name);
  }

  isBasisOfRecordSelected(id: number): boolean {
    return this.filterState.selectedBasisOfRecordIds().includes(id);
  }

  onBasisOfRecordToggle(id: number): void {
    this.filterState.toggleBasisOfRecord(id);
  }

  isRegistrationStatusSelected(id: number | null): boolean {
    return this.filterState.selectedRegistrationStatusId() === id;
  }

  onRegistrationStatusChange(id: number | null): void {
    this.filterState.setRegistrationStatus(id);
  }

  getBasisOfRecordDisplayName(basisOfRecord: BasisOfRecordDto): string {
    if (!basisOfRecord.name) return basisOfRecord.description ?? '';
    const key = 'sidebar.basisOfRecordName.' + basisOfRecord.name;
    const translated = this.translate.instant(key);
    return translated !== key ? translated : (basisOfRecord.description ?? basisOfRecord.name);
  }

  isTaxonGroupSelected(id: number): boolean {
    return this.filterState.selectedTaxonGroupIds().includes(id);
  }

  onTaxonGroupToggle(id: number): void {
    this.filterState.toggleTaxonGroup(id);
  }

  // Taxon tree lazy load
  readonly taxonTreeOpened = signal(false);

  onTaxonTreeToggle(): void {
    this.taxonTreeOpened.set(true);
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

  // Free text is matched by the Lookup endpoints; the filter only ever gets ids.
  readonly searchProjects: AutocompleteSearch<OrganizationDto> = (term) => this.organizationService.searchProjects(term);
  readonly searchDatasets: AutocompleteSearch<OrganizationDto> = (term) => this.organizationService.searchDatasets(term);
  readonly searchCatalogNumbers: AutocompleteSearch<CatalogNumberMatchDto> = (term) => this.organizationService.searchCatalogNumbers(term);
  readonly trackByOrganizationId = (organization: OrganizationDto) => organization.id;
  readonly trackByCatalogNumber = (match: CatalogNumberMatchDto) => match.catalogNumber;

  onProjectSelected(organization: OrganizationDto): void {
    if (organization.id == null) return;
    this.filterState.addProject({ id: organization.id, name: organization.name ?? '' });
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

  readonly months = [
    { value: 1, labelKey: 'sidebar.months.january' },
    { value: 2, labelKey: 'sidebar.months.february' },
    { value: 3, labelKey: 'sidebar.months.march' },
    { value: 4, labelKey: 'sidebar.months.april' },
    { value: 5, labelKey: 'sidebar.months.may' },
    { value: 6, labelKey: 'sidebar.months.june' },
    { value: 7, labelKey: 'sidebar.months.july' },
    { value: 8, labelKey: 'sidebar.months.august' },
    { value: 9, labelKey: 'sidebar.months.september' },
    { value: 10, labelKey: 'sidebar.months.october' },
    { value: 11, labelKey: 'sidebar.months.november' },
    { value: 12, labelKey: 'sidebar.months.december' },
  ];

  isMonthSelected(month: number): boolean {
    return this.filterState.selectedMonths().includes(month);
  }

  onMonthToggle(month: number): void {
    this.filterState.toggleMonth(month);
  }
}
