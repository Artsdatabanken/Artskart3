import {
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  afterRenderEffect,
  computed,
  contentChild,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgTemplateOutlet } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, catchError, debounce, distinctUntilChanged, map, of, switchMap, timer } from 'rxjs';
import { AutocompleteOptionDirective, AutocompleteSearch } from './autocomplete-option.directive';

let nextInstanceId = 0;

// Below this, a top-layer popup opens above the field if there is more room there.
const MinSpaceBelow = 200;

@Component({
  selector: 'app-autocomplete',
  imports: [TranslateModule, NgTemplateOutlet],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './autocomplete.component.html',
  styleUrl: './autocomplete.component.css',
  host: {
    '(keydown)': 'onKeydown($event)',
    '(focusin)': 'onFocusin($event)',
    '(focusout)': 'onFocusout($event)',
    '(click)': 'onInputClick($event)',
  },
})
export class AutocompleteComponent<T> {
  private readonly injector = inject(Injector);
  private readonly hostEl = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly searchElement = viewChild.required<ElementRef<HTMLElement & { value: string }>>('search');

  readonly search = input.required<AutocompleteSearch<T>>();
  readonly label = input.required<string>();
  readonly placeholder = input<string>();
  readonly listLabel = input<string>();
  readonly minLength = input(2);
  readonly variant = input<'standalone' | 'with-button'>('standalone');
  /** Translation key for the no-results message. Gets the term as `searchTerm`. */
  readonly noResultsKey = input('autocomplete.noResultsFor');
  readonly trackBy = input<(item: T) => unknown>((item) => item);
  /**
   * Lets the popup grow to fit its options, at least as wide as the field. It is shown
   * in the top layer so a narrow, scrolling parent such as the sidebar can't clip it.
   */
  readonly fitContent = input(false);
  readonly selected = output<T>();

  protected readonly optionTemplate = contentChild.required<AutocompleteOptionDirective<T>>(AutocompleteOptionDirective);
  private readonly popup = viewChild<ElementRef<HTMLElement>>('popup');
  readonly optionIdPrefix = `autocomplete-${nextInstanceId++}-option-`;

  private readonly searchInput$ = new Subject<string>();
  readonly results = signal<T[]>([]);
  readonly showAutocomplete = signal(false);
  readonly showNoResults = signal(false);
  readonly searchTerm = signal('');
  readonly highlightedIndex = signal(-1);
  private readonly dismissed = signal(false);
  // The term the current results were fetched for. While a newer search is loading, it differs from searchTerm.
  private resultsTerm = '';
  private readonly layoutChanged = signal(0);
  private readonly fieldGeometry = computed(() => {
    this.showAutocomplete();
    this.showNoResults();
    this.layoutChanged();
    const host = this.hostEl.nativeElement;
    // Span the text input and the submit button, not adb-search's padded host.
    const searchShadow = this.searchElement().nativeElement.shadowRoot;
    const input = (searchShadow?.querySelector('.input-wrapper') ?? host).getBoundingClientRect();
    const button = searchShadow?.querySelector('.submit-button')?.getBoundingClientRect() ?? input;
    return { input, width: button.right - input.left, viewportHeight: host.ownerDocument.documentElement.clientHeight };
  });
  /** The top-layer popup opens above the field when there is too little room below it. */
  readonly opensAbove = computed(() => {
    if (!this.fitContent()) return false;
    const { input, viewportHeight } = this.fieldGeometry();
    const spaceBelow = viewportHeight - input.bottom;
    return spaceBelow < MinSpaceBelow && input.top > spaceBelow;
  });
  readonly popupStyle = computed((): Record<string, string> => {
    const { input, width, viewportHeight } = this.fieldGeometry();
    if (!this.fitContent()) {
      const hostRect = this.hostEl.nativeElement.getBoundingClientRect();
      return { top: `${input.bottom - hostRect.top}px`, left: `${input.left - hostRect.left}px`, width: `${width}px` };
    }
    const above = this.opensAbove();
    const space = above ? input.top : viewportHeight - input.bottom;
    return {
      ...(above ? { bottom: `${viewportHeight - input.top}px` } : { top: `${input.bottom}px` }),
      left: `${input.left}px`,
      'min-width': `${width}px`,
      'max-width': `calc(100vw - ${input.left}px - var(--adb-spacing-sm))`,
      'max-height': `min(400px, calc(${space}px - var(--adb-spacing-sm)))`,
      visibility: this.isInView(input) ? 'visible' : 'hidden',
    };
  });

  constructor() {
    // The top-layer popup is positioned against the viewport, so it has to follow the field.
    const doc = this.hostEl.nativeElement.ownerDocument;
    const relayout = () => {
      if (this.fitContent() && (this.showAutocomplete() || this.showNoResults())) this.layoutChanged.update((n) => n + 1);
    };
    // Scrolling the popup's own list doesn't move the field.
    const relayoutOnScroll = (event: Event) => {
      if (!this.popup()?.nativeElement.contains(event.target as Node)) relayout();
    };
    doc.addEventListener('scroll', relayoutOnScroll, { capture: true, passive: true });
    doc.defaultView?.addEventListener('resize', relayout, { passive: true });
    inject(DestroyRef).onDestroy(() => {
      doc.removeEventListener('scroll', relayoutOnScroll, { capture: true });
      doc.defaultView?.removeEventListener('resize', relayout);
    });

    // Runs once per popup element: @if creates a new one each time the popup opens.
    afterRenderEffect(() => {
      const popup = this.popup()?.nativeElement;
      if (popup?.popover) popup.showPopover();
    });

    this.searchInput$
      .pipe(
        // distinctUntilChanged must precede debounceTime so the '' pushed by
        // select/onSearchClear resets its memory immediately; otherwise
        // retyping the same term after a selection is swallowed and no request fires.
        distinctUntilChanged(),
        // Cancelling emissions (short/empty terms) bypass the debounce so they
        // reach the switchMap within one macrotask instead of after 300 ms —
        // closing the window where an in-flight response could reopen a cleared popup.
        debounce((term) => timer(this.isSearchable(term) ? 300 : 0)),
        // The length guard lives inside the switchMap so that short/empty terms
        // still cancel any in-flight request instead of letting its late response
        // resurrect a cleared popup. null marks "cleared", distinct from a
        // completed search that happened to return zero results. A failed request
        // also clears, so it isn't reported as "no results".
        switchMap((term) =>
          this.isSearchable(term)
            ? this.search()(term).pipe(
                map((results) => ({ term, results })),
                catchError(() => of(null)),
              )
            : of(null),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((response) => {
        // Also while dismissed: results for an older term must not come back on reopen.
        if (response === null) {
          this.clearResults();
          return;
        }
        const { term, results } = response;
        this.resultsTerm = term;
        // Focus sitting on an option is destroyed when @for replaces the list.
        const hadOptionFocus = this.hostEl.nativeElement.ownerDocument.activeElement?.id.startsWith(this.optionIdPrefix) ?? false;
        if (this.dismissed()) {
          // Keep the payload fresh for an ArrowDown reopen, but stay closed.
          this.results.set(results);
          return;
        }
        this.results.set(results);
        this.showAutocomplete.set(results.length > 0);
        this.showNoResults.set(results.length === 0);
        this.highlightedIndex.set(-1);
        if (hadOptionFocus) {
          if (results.length > 0) {
            this.moveHighlight(() => 0);
          } else {
            this.focusSearchInput();
          }
        }
      });

    // The popup is sized in pixels, so it has to follow the field, e.g. while the sidebar is resized.
    const ResizeObserverCtor = this.hostEl.nativeElement.ownerDocument.defaultView?.ResizeObserver;
    if (ResizeObserverCtor) {
      const resizeObserver = new ResizeObserverCtor(() => this.layoutChanged.update((n) => n + 1));
      resizeObserver.observe(this.hostEl.nativeElement);
      inject(DestroyRef).onDestroy(() => resizeObserver.disconnect());
    }
  }

  private isSearchable(term: string): boolean {
    return term.length >= this.minLength();
  }

  // Nothing clips the top-layer popup, so it is hidden once the field is scrolled fully out of view.
  private isInView(inputRect: DOMRect): boolean {
    const view = this.hostEl.nativeElement.ownerDocument.defaultView;
    for (let el = this.hostEl.nativeElement.parentElement; el; el = el.parentElement) {
      if (view?.getComputedStyle(el).overflowY !== 'visible') {
        const clip = el.getBoundingClientRect();
        return inputRect.bottom > clip.top && inputRect.top < clip.bottom;
      }
    }
    return true;
  }

  onSearchInput(event: Event): void {
    const detail = (event as CustomEvent<{ value: string }>).detail;
    const value = detail.value.trim();
    this.searchTerm.set(value);
    this.highlightedIndex.set(-1);
    this.dismissed.set(false);
    if (!this.isSearchable(value)) {
      this.clearResults();
    }
    this.searchInput$.next(value);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.showAutocomplete() || this.showNoResults()) {
        event.preventDefault();
        event.stopPropagation();
        this.dismiss();
        this.focusSearchInput();
      }
      return;
    }
    const count = this.results().length;
    if (count === 0) return;
    if (!this.showAutocomplete()) {
      // APG combobox: ArrowDown reopens a popup dismissed with Escape when results remain.
      if (event.key === 'ArrowDown') {
        event.preventDefault();
        this.reopen();
        this.moveHighlight(() => 0);
      }
      return;
    }
    // Roving focus: arrows move both the highlight and DOM focus between options.
    // The list lives in the light DOM while the input sits in adb-search's shadow
    // root, so aria-activedescendant cannot bridge them — real focus is required.
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveHighlight((i) => (i + 1) % count);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveHighlight((i) => (i <= 0 ? count - 1 : i - 1));
    }
    // Enter is not handled here: in the input it fires adb-search → onSearchSubmit
    // before bubbling, and on an option the li's own (keydown.enter) handles it.
  }

  // Only focus arriving from outside reopens: Escape moves focus from an option
  // back to the input, and that must not undo the dismissal.
  onFocusin(event: FocusEvent): void {
    if (this.hostEl.nativeElement.contains(event.relatedTarget as Node | null)) return;
    if (this.results().length > 0) this.reopen();
  }

  onFocusout(event: FocusEvent): void {
    if (this.hostEl.nativeElement.contains(event.relatedTarget as Node | null)) return;
    // Chrome also fires focusout when a focused option is removed because new results
    // replaced the list. By the next microtask that option is detached, so it can be told
    // apart from focus really leaving.
    const target = event.target as Node;
    void Promise.resolve().then(() => {
      if (target.isConnected) this.dismiss();
    });
  }

  // After Escape the input keeps focus, so a click on it fires no focusin.
  onInputClick(event: MouseEvent): void {
    if (event.composedPath()[0] instanceof HTMLInputElement && this.results().length > 0) this.reopen();
  }

  private reopen(): void {
    this.dismissed.set(false);
    this.showAutocomplete.set(true);
    this.showNoResults.set(false);
  }

  private dismiss(): void {
    this.dismissed.set(true);
    this.hidePopup();
  }

  private hidePopup(): void {
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    this.highlightedIndex.set(-1);
  }

  private clearResults(): void {
    this.results.set([]);
    this.hidePopup();
  }

  private moveHighlight(update: (index: number) => number): void {
    this.highlightedIndex.update(update);
    // Signal writes only schedule change detection, so the option may not be in
    // the DOM yet (e.g. when ArrowDown reopens a dismissed popup). Focus after render.
    afterNextRender(
      () => {
        this.hostEl.nativeElement.querySelector<HTMLElement>(`#${this.optionIdPrefix}${this.highlightedIndex()}`)?.focus();
      },
      { injector: this.injector },
    );
  }

  private focusSearchInput(): void {
    const search = this.searchElement().nativeElement;
    (search.shadowRoot?.querySelector('input') ?? search).focus();
  }

  onSearchClear(): void {
    this.searchTerm.set('');
    this.clearResults();
    // Cancel any in-flight request and reset the distinctUntilChanged memory.
    this.searchInput$.next('');
  }

  onSearchSubmit(): void {
    // A popup dismissed with Escape must not resurrect its stale results on Enter.
    if (!this.showAutocomplete()) return;
    // Nor may Enter pick from results for an earlier term while the new search is loading.
    if (this.resultsTerm !== this.searchTerm()) return;
    const results = this.results();
    if (results.length === 0) return;
    const index = this.highlightedIndex();
    this.select(index >= 0 && index < results.length ? results[index] : results[0]);
  }

  select(item: T): void {
    this.selected.emit(item);
    this.onSearchClear();
    this.searchElement().nativeElement.value = '';
    this.focusSearchInput();
  }
}
