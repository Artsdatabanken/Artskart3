import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, DestroyRef, inject, signal, computed } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
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
import { OrganizationService } from '../../services/organization/organization.service';
import { FilterStateService, ImageFilterOption } from '../../services/filter-state/filter-state.service';
import { FilterChipsComponent } from '../filter-chips/filter-chips.component';
import { SpeciesSearchComponent } from '../species-search/species-search.component';
import { TaxonTreeComponent } from '../taxon-tree/taxon-tree.component';
import type { components } from '../../types/api.generated';

const MinProjectNameSearchLength = 2;

interface RegistreringOption {
  id: number | null;
  labelKey: string;
  descriptionKey?: string;
}

@Component({
  selector: 'app-sidebar',
  imports: [TranslateModule, FormatNumberPipe, FilterChipsComponent, SpeciesSearchComponent, TaxonTreeComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  private readonly destroyRef = inject(DestroyRef);
  protected readonly translate = inject(TranslateService);

  // Samling, prosjekt og katalognummer er typeahead-felt: brukeren skriver
  // fritekst, velger et treff, og filteret får en ID. Fritekstsøket skjer i
  // Lookup-endepunktene mot små tabeller eller en indeks — aldri i selve
  // søkespørringen mot 61M observasjoner.
  private readonly datasetSearch$ = new Subject<string>();
  readonly datasetSuggestions = signal<components['schemas']['OrganizationDto'][]>([]);
  readonly showDatasetSuggestions = signal<boolean>(false);

  private readonly collectionSearch$ = new Subject<string>();
  readonly collectionSuggestions = signal<components['schemas']['OrganizationDto'][]>([]);
  readonly showCollectionSuggestions = signal<boolean>(false);

  private readonly catalogNumberSearch$ = new Subject<string>();
  readonly catalogNumberSuggestions = signal<components['schemas']['CatalogNumberMatchDto'][]>([]);
  readonly showCatalogNumberSuggestions = signal<boolean>(false);

  // «Tekst skrevet, men ingen ID valgt» må være synlig.
  //
  // Filteret sender ID-er, ikke tekst. Uten dette kunne feltet vise
  // «Universitetsmuseet i Bergen» mens søket var helt ufiltrert — brukeren ser et
  // aktivt filter og får et resultat som ser plausibelt ut. Samme feilklasse som
  // takson-filteret som stille returnerte alle 60M observasjoner, bare flyttet til
  // presentasjonslaget.
  //
  // Gjelder også etter et valg: velger man «NHM» og deretter redigerer teksten til
  // «NIN», nullstilles ID-en, og da må feltet si fra.
  readonly datasetUnresolved = computed(
    () => this.datasetName().trim().length > 0 && this.datasetOrgId() === null,
  );
  readonly collectionUnresolved = computed(
    () => this.collectionName().trim().length > 0 && this.collectionOrgId() === null,
  );
  readonly catalogNumberUnresolved = computed(
    () => this.catalogNumber().trim().length > 0 && this.catalogObservationIds().length === 0,
  );

  // Skilles fra «venter fortsatt» slik at et søk uten treff ikke ser identisk ut
  // med et søk som ikke har rukket å svare.
  readonly datasetNoMatches = signal<boolean>(false);
  readonly collectionNoMatches = signal<boolean>(false);
  readonly catalogNumberNoMatches = signal<boolean>(false);

  constructor() {
    this.datasetSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          const trimmed = term.trim();
          if (trimmed.length < MinProjectNameSearchLength) {
            return of<components['schemas']['OrganizationDto'][]>([]);
          }
          return this.organizationService
            .searchDatasets(trimmed)
            .pipe(catchError(() => of<components['schemas']['OrganizationDto'][]>([])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((organizations) => {
        this.datasetSuggestions.set(organizations);
        this.showDatasetSuggestions.set(organizations.length > 0);
        this.datasetNoMatches.set(organizations.length === 0 && this.datasetUnresolved());
      });

    this.collectionSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          const trimmed = term.trim();
          if (trimmed.length < MinProjectNameSearchLength) {
            return of<components['schemas']['OrganizationDto'][]>([]);
          }
          return this.organizationService
            .searchCollections(trimmed)
            .pipe(catchError(() => of<components['schemas']['OrganizationDto'][]>([])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((organizations) => {
        this.collectionSuggestions.set(organizations);
        this.showCollectionSuggestions.set(organizations.length > 0);
        this.collectionNoMatches.set(organizations.length === 0 && this.collectionUnresolved());
      });

    this.catalogNumberSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          const trimmed = term.trim();
          if (trimmed.length < MinProjectNameSearchLength) {
            return of<components['schemas']['CatalogNumberMatchDto'][]>([]);
          }
          return this.organizationService
            .searchCatalogNumbers(trimmed)
            .pipe(catchError(() => of<components['schemas']['CatalogNumberMatchDto'][]>([])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((matches) => {
        this.catalogNumberSuggestions.set(matches);
        this.showCatalogNumberSuggestions.set(matches.length > 0);
        this.catalogNumberNoMatches.set(matches.length === 0 && this.catalogNumberUnresolved());
      });
  }
  readonly registreringOptions: RegistreringOption[] = [
    { id: null, labelKey: 'sidebar.registreringStatus.alle' },
    { id: 1, labelKey: 'sidebar.registreringStatus.present', descriptionKey: 'sidebar.registreringStatus.presentDescription' },
    { id: 2, labelKey: 'sidebar.registreringStatus.absent', descriptionKey: 'sidebar.registreringStatus.absentDescription' },
    { id: 3, labelKey: 'sidebar.registreringStatus.notrefound', descriptionKey: 'sidebar.registreringStatus.notrefoundDescription' },
  ];

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
    this.coordinatePrecisionFromInput.set('');
    this.coordinatePrecisionToInput.set('');
    this.periodFromInput.set('');
    this.periodToInput.set('');
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

  // Coordinate precision filter
  readonly coordinatePrecisionFromInput = signal('');
  readonly coordinatePrecisionToInput = signal('');

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
  readonly periodFromInput = signal('');
  readonly periodToInput = signal('');

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

  readonly datasetName = this.filterState.datasetName;
  readonly collectionName = this.filterState.collectionName;
  readonly catalogNumber = this.filterState.catalogNumber;
  // ID-signalene eksponeres for de tre *Unresolved-computedene over: teksten alene
  // sier ingenting om filteret faktisk er aktivt.
  readonly datasetOrgId = this.filterState.datasetOrgId;
  readonly collectionOrgId = this.filterState.collectionOrgId;
  readonly catalogObservationIds = this.filterState.catalogObservationIds;
  readonly imageFilter = this.filterState.imageFilter;

  // Felles for alle tre: å skrive i feltet nullstiller den valgte ID-en. Uten
  // det ville teksten og filteret kunne peke på hver sin ting — brukeren ser
  // «Fugler», men filteret står fortsatt på forrige valg.
  onDatasetNameChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.filterState.setDatasetName(input.value);
    this.filterState.setDatasetOrgId(null);
    this.datasetSearch$.next(input.value);
  }

  onDatasetNameFocus(): void {
    if (this.datasetSuggestions().length > 0) {
      this.showDatasetSuggestions.set(true);
    }
  }

  onDatasetNameBlur(): void {
    // Delay hiding so a (mousedown) selection on a suggestion registers first.
    setTimeout(() => this.showDatasetSuggestions.set(false), 150);
  }

  selectDatasetSuggestion(organization: components['schemas']['OrganizationDto']): void {
    this.filterState.setDatasetName(organization.name ?? '');
    this.filterState.setDatasetOrgId(organization.id ?? null);
    this.datasetSuggestions.set([]);
    this.showDatasetSuggestions.set(false);
  }

  onCollectionNameChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.filterState.setCollectionName(input.value);
    this.filterState.setCollectionOrgId(null);
    this.collectionSearch$.next(input.value);
  }

  onCollectionNameFocus(): void {
    if (this.collectionSuggestions().length > 0) {
      this.showCollectionSuggestions.set(true);
    }
  }

  onCollectionNameBlur(): void {
    setTimeout(() => this.showCollectionSuggestions.set(false), 150);
  }

  selectCollectionSuggestion(organization: components['schemas']['OrganizationDto']): void {
    this.filterState.setCollectionName(organization.name ?? '');
    this.filterState.setCollectionOrgId(organization.id ?? null);
    this.collectionSuggestions.set([]);
    this.showCollectionSuggestions.set(false);
  }

  onCatalogNumberChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.filterState.setCatalogNumber(input.value);
    this.filterState.setCatalogObservationIds([]);
    this.catalogNumberSearch$.next(input.value);
  }

  onCatalogNumberFocus(): void {
    if (this.catalogNumberSuggestions().length > 0) {
      this.showCatalogNumberSuggestions.set(true);
    }
  }

  onCatalogNumberBlur(): void {
    setTimeout(() => this.showCatalogNumberSuggestions.set(false), 150);
  }

  // Treffet bærer ObservationId-ene med seg, så det trengs ikke noe ekstra kall
  // for å gjøre om katalognummeret til et filter.
  selectCatalogNumberSuggestion(match: components['schemas']['CatalogNumberMatchDto']): void {
    this.filterState.setCatalogNumber(match.catalogNumber ?? '');
    this.filterState.setCatalogObservationIds(match.observationIds ?? []);
    this.catalogNumberSuggestions.set([]);
    this.showCatalogNumberSuggestions.set(false);
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
