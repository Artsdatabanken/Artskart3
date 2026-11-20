import { Component, CUSTOM_ELEMENTS_SCHEMA, ElementRef, Injector, afterNextRender, computed, inject, viewChild } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CustomAreaDrawingService } from '../../services/custom-area-drawing/custom-area-drawing.service';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';

/**
 * Buttons for drawing a custom area on the map. The same button starts and finishes the drawing,
 * and removes the drawn polygon, whether it's in the filter or rejected. The red button cancels a drawing.
 */
@Component({
  selector: 'app-custom-area-control',
  imports: [TranslateModule, FormatNumberPipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  template: `
    <p class="description">{{ 'sidebar.customArea.description' | translate }}</p>
    <div class="actions">
      <adb-button
        #mainButton
        variant="secondary"
        size="sm"
        [attr.color]="removesArea() ? 'danger' : null"
        [disabled]="drawing.isDrawing() && !drawing.canFinish()"
        (click)="mainAction()"
      >
        @if (drawing.isDrawing()) {
          <svg slot="icon-start" class="icon" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" viewBox="0 -960 960 960">
            <path d="M382-240 154-468l57-57 171 171 367-367 57 57-424 424Z" fill="currentColor" />
          </svg>
          {{ 'sidebar.customArea.finish' | translate }}
        } @else if (removesArea()) {
          <svg slot="icon-start" class="icon" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" viewBox="0 -960 960 960">
            <path d="M280-120q-33 0-56.5-23.5T200-200v-520h-40v-80h200v-40h240v40h200v80h-40v520q0 33-23.5 56.5T680-120H280Zm400-600H280v520h400v-520ZM360-280h80v-360h-80v360Zm160 0h80v-360h-80v360Z" fill="currentColor" />
          </svg>
          {{ 'sidebar.customArea.remove' | translate }}
        } @else {
          <adb-icon slot="icon-start" name="custom/polygon" aria-hidden="true" />
          {{ 'sidebar.customArea.draw' | translate }}
        }
      </adb-button>
      @if (drawing.isDrawing()) {
        <adb-icon-button variant="secondary" size="sm" (click)="drawing.undo()" [attr.aria-label]="'sidebar.customArea.undo' | translate">
          <svg class="icon" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" viewBox="0 -960 960 960">
            <path
              d="M280-200v-80h284q63 0 109.5-40T720-420q0-60-46.5-100T564-560H312l104 104-56 56-200-200 200-200 56 56-104 104h252q97 0 166.5 63T800-420q0 94-69.5 157T564-200H280Z"
              fill="currentColor"
            />
          </svg>
        </adb-icon-button>
      }
      @if (drawing.isDrawing()) {
        <adb-icon-button
          variant="secondary"
          color="danger"
          size="sm"
          (click)="cancel()"
          [attr.aria-label]="'sidebar.customArea.cancel' | translate"
        >
          <svg class="icon" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" viewBox="0 -960 960 960">
            <path
              d="M280-120q-33 0-56.5-23.5T200-200v-520h-40v-80h200v-40h240v40h200v80h-40v520q0 33-23.5 56.5T680-120H280Zm400-600H280v520h400v-520ZM360-280h80v-360h-80v360Zm160 0h80v-360h-80v360Z"
              fill="currentColor"
            />
          </svg>
        </adb-icon-button>
      }
    </div>
    <!-- Live region must exist in the DOM before a message arrives, or it isn't announced. -->
    <p class="message" role="status">
      @if (drawing.rejected(); as rejected) {
        {{ 'sidebar.customArea.' + rejected.reason | translate: { max: (rejected.limit ?? 0 | formatNumber: translate.currentLang) } }}
      }
    </p>
  `,
  styles: `
    :host {
      display: block;
    }

    .description {
      margin: 0 0 var(--adb-spacing-2xsm);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--adb-spacing-2xsm);
    }

    .message {
      margin: var(--adb-spacing-2xsm) 0 0;
      font-size: var(--adb-font-size-3);
      color: var(--adb-text-brand-1);
    }

    .message:empty {
      display: none;
    }

    .icon {
      width: var(--adb-icon-size, 1.25em);
      height: var(--adb-icon-size, 1.25em);
    }
  `,
})
export class CustomAreaControlComponent {
  protected readonly drawing = inject(CustomAreaDrawingService);
  protected readonly translate = inject(TranslateService);
  private readonly mainButton = viewChild.required<ElementRef<HTMLElement>>('mainButton');
  private readonly injector = inject(Injector);

  protected readonly removesArea = computed(() => !this.drawing.isDrawing() && this.drawing.hasArea());

  mainAction(): void {
    if (this.drawing.isDrawing()) this.drawing.finish();
    else if (this.removesArea()) this.drawing.remove();
    else this.drawing.start();
  }

  cancel(): void {
    this.drawing.cancel();
    // The red button goes away, so focus moves to the main button once it is enabled again.
    afterNextRender(
      async () => {
        const button = this.mainButton().nativeElement as HTMLElement & { updateComplete?: Promise<unknown> };
        await button.updateComplete;
        button.shadowRoot?.querySelector('button')?.focus();
      },
      { injector: this.injector },
    );
  }
}
