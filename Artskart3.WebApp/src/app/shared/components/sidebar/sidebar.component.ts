import {
  Component,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
  computed,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { CategoryService } from '../../services/category/category.service';
import { AreaService } from '../../services/area/area.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { AreaDto, AreaTypeDto, CategoryTypeDto } from '../../types/api.types';

export interface CountyGroup {
  county: AreaDto;
  municipalities: AreaDto[];
}

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
  private readonly filterState = inject(FilterStateService);

  readonly categoriesResource = rxResource<CategoryTypeDto[], void>({
    stream: () => this.categoryService.getCategories(),
  });

  readonly areasResource = rxResource<AreaTypeDto[], void>({
    stream: () => this.areaService.getAreas(),
  });

  readonly categoryTypes = this.categoriesResource.value;

  readonly countyGroups = computed<CountyGroup[]>(() => {
    const areaTypes = this.areasResource.value();
    if (!areaTypes) return [];

    const countyType = areaTypes.find((t) => t.name === 'County');
    const municipalityType = areaTypes.find((t) => t.name === 'Municipality');

    const counties = (countyType?.areas ?? []).filter((a) => a.fid && a.isCurrent);
    const municipalities = (municipalityType?.areas ?? []).filter((a) => a.fid && a.isCurrent);

    return counties.map((county) => ({
      county,
      municipalities: municipalities.filter((m) => m.fid!.substring(0, 2) === county.fid),
    }));
  });

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
}
