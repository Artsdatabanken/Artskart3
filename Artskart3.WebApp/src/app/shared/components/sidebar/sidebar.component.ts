import {
  Component,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CategoryService } from '../../services/category/category.service';
import { AreaService, CountyGroup } from '../../services/area/area.service';
import { InstitutionService } from '../../services/institution/institution.service';
import { BehaviorService } from '../../services/behavior/behavior.service';
import { BasisOfRecordService } from '../../services/basis-of-record/basis-of-record.service';
import { TaxonGroupService } from '../../services/taxon-group/taxon-group.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { BehaviorDto, BasisOfRecordDto, CategoryTypeDto, InstitutionDto, TaxonGroupDto } from '../../types/api.types';

@Component({
  selector: 'app-sidebar',
  imports: [TranslateModule],
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
  private readonly filterState = inject(FilterStateService);
  private readonly translate = inject(TranslateService);

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
  readonly basisOfRecords = this.basisOfRecordsResource.value;
  readonly taxonGroups = this.taxonGroupsResource.value;
  readonly countyGroups = this.areaService.countyGroups;

  isCategorySelected(id: number): boolean {
    return this.filterState.selectedCategoryIds().includes(id);
  }

  isAllInTypeSelected(type: CategoryTypeDto): boolean {
    const ids = (type.categories ?? []).filter((c) => c.id !== undefined).map((c) => c.id as number);
    if (ids.length === 0) return false;
    const selected = this.filterState.selectedCategoryIds();
    return ids.every((id) => selected.includes(id));
  }

  isSomeInTypeSelected(type: CategoryTypeDto): boolean {
    const ids = (type.categories ?? []).filter((c) => c.id !== undefined).map((c) => c.id as number);
    const selected = this.filterState.selectedCategoryIds();
    const count = ids.filter((id) => selected.includes(id)).length;
    return count > 0 && count < ids.length;
  }

  onCategoryToggle(id: number): void {
    this.filterState.toggleCategory(id);
  }

  onClearFilter(): void {
    this.filterState.clearAll();
    this.coordinatePrecisionFromInput.set('');
    this.coordinatePrecisionToInput.set('');
    this.periodFromInput.set('');
    this.periodToInput.set('');
  }

  onTypeToggle(type: CategoryTypeDto): void {
    const ids = (type.categories ?? []).filter((c) => c.id !== undefined).map((c) => c.id as number);
    if (this.isAllInTypeSelected(type)) {
      ids.forEach((id) => this.filterState.removeCategory(id));
    } else {
      ids.forEach((id) => this.filterState.addCategory(id));
    }
  }

  isMunicipalitySelected(fid: string): boolean {
    return this.filterState.selectedMunicipalityIds().includes(fid);
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
}
