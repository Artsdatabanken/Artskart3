import {
  Component,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { CategoryService } from '../../services/category/category.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { CategoryTypeDto } from '../../types/api.types';

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
  private readonly filterState = inject(FilterStateService);

  readonly categoriesResource = rxResource<CategoryTypeDto[], void>({
    stream: () => this.categoryService.getCategories(),
  });

  readonly categoryTypes = this.categoriesResource.value;

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
    this.filterState.clearCategories();
  }

  onTypeToggle(type: CategoryTypeDto): void {
    const ids = (type.categories ?? []).filter((c) => c.id !== undefined).map((c) => c.id as number);
    if (this.isAllInTypeSelected(type)) {
      ids.forEach((id) => this.filterState.removeCategory(id));
    } else {
      ids.forEach((id) => this.filterState.addCategory(id));
    }
  }
}
