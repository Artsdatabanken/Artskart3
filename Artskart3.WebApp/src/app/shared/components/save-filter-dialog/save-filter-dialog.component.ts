import { Component, CUSTOM_ELEMENTS_SCHEMA, inject, model, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ModalComponent } from '../modal/modal.component';
import { SavedFilterService } from '../../services/saved-filter/saved-filter.service';
import { AlertService } from '../../services/alert/alert.service';
import { apiErrorMessage } from '../../utils/api-error';

@Component({
  selector: 'app-save-filter-dialog',
  imports: [ModalComponent, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './save-filter-dialog.component.html',
  styleUrl: './save-filter-dialog.component.css',
})
export class SaveFilterDialogComponent {
  private readonly savedFilterService = inject(SavedFilterService);
  private readonly alertService = inject(AlertService);
  private readonly translate = inject(TranslateService);

  readonly open = model(false);
  readonly name = signal('');
  readonly isDefault = signal(false);
  readonly saving = signal(false);

  onConfirm(): void {
    const name = this.name().trim();
    if (!name || this.saving()) return;

    this.saving.set(true);
    this.savedFilterService.create(name, this.isDefault()).subscribe({
      next: () => {
        this.alertService.showSuccess(this.translate.instant('savedFilters.saved', { name }));
        this.close();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.alertService.showError(apiErrorMessage(error, this.translate.instant('savedFilters.saveFailed')));
      },
    });
  }

  close(): void {
    this.open.set(false);
    this.name.set('');
    this.isDefault.set(false);
    this.saving.set(false);
  }
}
