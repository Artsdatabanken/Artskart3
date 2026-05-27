import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, computed, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-pagination',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.css',
})
export class PaginationComponent {
  readonly currentPage = input.required<number>();
  readonly totalVisiblePages = input.required<number>();
  readonly hasMorePages = input(false);
  readonly pageSizeOptions = input<number[]>([]);
  readonly pageSize = input<number>(0);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly showPageSizeSelector = computed(() => this.pageSizeOptions().length > 0);

  readonly rangeLabel = computed(() => {
    const size = this.pageSize();
    const page = this.currentPage();
    if (!size) return '';
    const start = (page - 1) * size + 1;
    const end = page * size;
    return `${start}-${end}`;
  });

  readonly visiblePages = computed(() => {
    const current = this.currentPage();
    const total = this.totalVisiblePages();
    const maxVisible = 5;

    let start = Math.max(1, current - Math.floor(maxVisible / 2));
    let end = start + maxVisible - 1;

    if (end > total) {
      end = total;
      start = Math.max(1, end - maxVisible + 1);
    }

    const pages: number[] = [];
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  readonly showLeadingEllipsis = computed(() => this.visiblePages()[0] > 1);

  readonly hasPreviousPage = computed(() => this.currentPage() > 1);

  readonly hasNextPage = computed(() => {
    return this.currentPage() < this.totalVisiblePages() || this.hasMorePages();
  });

  goToPage(page: number) {
    this.pageChange.emit(page);
  }

  nextPage() {
    this.pageChange.emit(this.currentPage() + 1);
  }

  previousPage() {
    this.pageChange.emit(Math.max(1, this.currentPage() - 1));
  }

  onPageSizeChange(event: Event) {
    const value = Number((event.target as HTMLSelectElement).value);
    this.pageSizeChange.emit(value);
  }
}
