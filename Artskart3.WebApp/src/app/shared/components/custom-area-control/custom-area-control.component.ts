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
          <adb-icon slot="icon-start" class="icon" name="check" aria-hidden="true" />
          {{ 'sidebar.customArea.finish' | translate }}
        } @else if (removesArea()) {
          <adb-icon slot="icon-start" class="icon" name="delete" aria-hidden="true" />
          {{ 'sidebar.customArea.remove' | translate }}
        } @else {
          <adb-icon slot="icon-start" name="custom/polygon" aria-hidden="true" />
          {{ 'sidebar.customArea.draw' | translate }}
        }
      </adb-button>
      @if (drawing.isDrawing()) {
        <adb-icon-button variant="secondary" size="sm" (click)="drawing.undo()" [attr.aria-label]="'sidebar.customArea.undo' | translate">
          <adb-icon class="icon" name="undo" aria-hidden="true" />
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
          <adb-icon class="icon" name="delete" aria-hidden="true" />
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
