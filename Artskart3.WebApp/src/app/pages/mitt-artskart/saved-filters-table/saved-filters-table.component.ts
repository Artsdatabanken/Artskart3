import { Component, CUSTOM_ELEMENTS_SCHEMA, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, finalize } from 'rxjs';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { LocaleDateTimePipe } from '../../../shared/pipes/locale-date-time.pipe';
import { AlertService } from '../../../shared/services/alert/alert.service';
import { SavedFilterService } from '../../../shared/services/saved-filter/saved-filter.service';
import { SavedFilterDto } from '../../../shared/types/api.types';
import { apiErrorMessage } from '../../../shared/utils/api-error';

@Component({
  selector: 'app-saved-filters-table',
  imports: [TranslateModule, RouterLink, LocaleDateTimePipe, ModalComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './saved-filters-table.component.html',
  styleUrls: ['../data-table.css', './saved-filters-table.component.css'],
})
export class SavedFiltersTableComponent {
  protected readonly savedFilterService = inject(SavedFilterService);
  protected readonly translate = inject(TranslateService);
  private readonly alertService = inject(AlertService);

  readonly filterToDelete = signal<SavedFilterDto | null>(null);
  readonly updatingId = signal<string | null>(null);
  readonly deleting = signal(false);

  onToggleDefault(savedFilter: SavedFilterDto): void {
    if (!savedFilter.id || this.updatingId()) return;
    const request: Observable<void> = savedFilter.isDefault
      ? this.savedFilterService.clearDefault(savedFilter.id)
      : this.savedFilterService.setDefault(savedFilter.id);

    this.updatingId.set(savedFilter.id);
    request.pipe(finalize(() => this.updatingId.set(null))).subscribe({
      error: (error: unknown) =>
        this.alertService.showError(apiErrorMessage(error, this.translate.instant('savedFilters.updateFailed'))),
    });
  }

  onConfirmDelete(): void {
    const savedFilter = this.filterToDelete();
    if (!savedFilter?.id || this.deleting()) return;

    this.deleting.set(true);
    this.savedFilterService.delete(savedFilter.id).subscribe({
      next: () => {
        this.closeDeleteModal();
        this.alertService.showSuccess(this.translate.instant('savedFilters.deleted'));
      },
      error: (error: unknown) => {
        this.closeDeleteModal();
        this.alertService.showError(apiErrorMessage(error, this.translate.instant('savedFilters.deleteFailed')));
      },
    });
  }

  closeDeleteModal(): void {
    this.filterToDelete.set(null);
    this.deleting.set(false);
  }
}
