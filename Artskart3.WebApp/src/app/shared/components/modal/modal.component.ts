import {
  ChangeDetectionStrategy,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  ElementRef,
  input,
  output,
  effect,
  viewChild,
} from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

export type ModalVariant = 'prompt' | 'info';

@Component({
  selector: 'app-modal',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './modal.component.html',
  styleUrl: './modal.component.css',
  host: {
    '[attr.aria-hidden]': '!open()',
  },
})
export class ModalComponent {
  private static nextId = 0;
  readonly titleId = `modal-title-${ModalComponent.nextId++}`;

  readonly open = input.required<boolean>();
  readonly title = input.required<string>();
  readonly variant = input<ModalVariant>('info');
  readonly confirmLabel = input('OK');
  readonly cancelLabel = input<string>('Avbryt');
  readonly confirmDisabled = input(false);
  readonly loading = input(false);

  readonly modalConfirm = output<void>();
  readonly modalCancel = output<void>();

  readonly dialogRef = viewChild<ElementRef<HTMLElement>>('dialog');

  private previouslyFocusedElement: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.previouslyFocusedElement = document.activeElement as HTMLElement;
        // Wait for DOM to render before focusing
        setTimeout(() => this.focusFirstElement());
      } else if (this.previouslyFocusedElement) {
        this.previouslyFocusedElement.focus();
        this.previouslyFocusedElement = null;
      }
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.emitCancel();
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.emitCancel();
      return;
    }

    if (event.key === 'Tab') {
      this.trapFocus(event);
    }
  }

  emitConfirm(): void {
    if (this.confirmDisabled() || this.loading()) return;
    this.modalConfirm.emit();
  }

  emitCancel(): void {
    this.modalCancel.emit();
  }

  private focusFirstElement(): void {
    const dialog = this.dialogRef()?.nativeElement;
    if (!dialog) return;

    const focusable = dialog.querySelectorAll<HTMLElement>(
      'input, button, [tabindex]:not([tabindex="-1"]), adb-button, textarea, select',
    );
    if (focusable.length > 0) {
      focusable[0].focus();
    }
  }

  private trapFocus(event: KeyboardEvent): void {
    const dialog = this.dialogRef()?.nativeElement;
    if (!dialog) return;

    const focusableElements = dialog.querySelectorAll<HTMLElement>(
      'input, button, [tabindex]:not([tabindex="-1"]), adb-button, textarea, select',
    );
    if (focusableElements.length === 0) return;

    const first = focusableElements[0];
    const last = focusableElements[focusableElements.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
