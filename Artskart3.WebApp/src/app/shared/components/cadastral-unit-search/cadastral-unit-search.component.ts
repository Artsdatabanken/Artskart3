import { HttpErrorResponse } from '@angular/common/http';
import { Component, CUSTOM_ELEMENTS_SCHEMA, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { CadastralUnitService, normalizeCadastralUnitId } from '../../services/cadastral-unit/cadastral-unit.service';
import { CadastralUnitDto } from '../../types/api.types';

interface Message {
  key: string;
  params?: Record<string, string>;
}

/**
 * A field for a whole matrikkelnummer, looked up when submitted. Kartverket can't search
 * on part of a number, so there are no suggestions while typing.
 */
@Component({
  selector: 'app-cadastral-unit-search',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  template: `
    <adb-search
      #search
      size="sm"
      variant="standalone"
      placeholder=""
      [attr.label]="label()"
      (adb-input)="onSearchInput($event)"
      (adb-search)="onSearchSubmit()"
      (adb-clear)="onSearchClear()"
    ></adb-search>
    <p class="message" [class.message--error]="error()">
      {{ error()?.key ?? 'sidebar.cadastralUnitHint' | translate: error()?.params }}
    </p>
    <!-- Live region must exist in the DOM before a message arrives, or it isn't announced. -->
    <div class="sr-only" role="status" aria-atomic="true">
      @if (error(); as error) {
        {{ error.key | translate: error.params }}
      }
    </div>
  `,
  styleUrl: './cadastral-unit-search.component.css',
})
export class CadastralUnitSearchComponent {
  private readonly cadastralUnits = inject(CadastralUnitService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchElement = viewChild.required<ElementRef<HTMLElement & { value: string }>>('search');

  readonly label = input.required<string>();
  readonly selected = output<CadastralUnitDto>();

  private readonly value = signal('');
  protected readonly error = signal<Message | null>(null);
  private lookup?: Subscription;

  onSearchInput(event: Event): void {
    this.lookup?.unsubscribe();
    this.value.set((event as CustomEvent<{ value: string }>).detail.value);
    this.error.set(null);
  }

  onSearchClear(): void {
    this.lookup?.unsubscribe();
    this.value.set('');
    this.error.set(null);
  }

  onSearchSubmit(): void {
    if (!this.value().trim()) return;

    const id = normalizeCadastralUnitId(this.value());
    if (!id) {
      this.error.set({ key: 'sidebar.cadastralUnitInvalidFormat' });
      return;
    }

    this.lookup?.unsubscribe();
    this.lookup = this.cadastralUnits
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (cadastralUnit) => {
          this.selected.emit(cadastralUnit);
          this.onSearchClear();
          this.searchElement().nativeElement.value = '';
        },
        error: (error: unknown) =>
          this.error.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? { key: 'sidebar.cadastralUnitNotFound', params: { id } }
              : { key: 'sidebar.cadastralUnitLookupFailed' },
          ),
      });
  }
}
