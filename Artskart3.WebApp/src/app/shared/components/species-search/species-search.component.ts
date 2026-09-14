import {
  Component,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
  signal,
  computed,
  afterNextRender,
  DestroyRef,
  Injector,
  OnInit,
  ElementRef,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, of, timer, switchMap, debounce, distinctUntilChanged } from 'rxjs';
import { SpeciesSearchService } from '../../services/species-search/species-search.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { SpeciesDto } from '../../types/api.types';

@Component({
  selector: 'app-species-search',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './species-search.component.html',
  styleUrl: './species-search.component.css',
  host: {
    '(keydown)': 'onKeydown($event)',
  },
})
export class SpeciesSearchComponent implements OnInit {
  private readonly speciesSearchService = inject(SpeciesSearchService);
  private readonly filterState = inject(FilterStateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);

  private readonly searchField = viewChild<ElementRef<HTMLElement>>('searchField');
  private readonly hostEl = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly searchInput$ = new Subject<string>();
  readonly speciesResults = signal<SpeciesDto[]>([]);
  readonly showAutocomplete = signal(false);
  readonly showNoResults = signal(false);
  readonly searchTerm = signal('');
  readonly highlightedIndex = signal(-1);
  private readonly dismissed = signal(false);
  readonly autocompletePosition = computed(() => {
    this.showAutocomplete();
    const el = this.searchField()?.nativeElement;
    if (!el) return { top: 0, left: 0, width: 0 };
    const rect = el.getBoundingClientRect();
    return { top: rect.bottom, left: rect.left, width: rect.width };
  });

  ngOnInit(): void {
    this.searchInput$
      .pipe(
        // distinctUntilChanged must precede debounceTime so the '' pushed by
        // onSpeciesSelect/onSearchClear resets its memory immediately; otherwise
        // retyping the same term after a selection is swallowed and no request fires.
        distinctUntilChanged(),
        // Cancelling emissions (short/empty terms) bypass the debounce so they
        // reach the switchMap within one macrotask instead of after 300 ms —
        // closing the window where an in-flight response could reopen a cleared popup.
        debounce((term) => timer(term.length >= 2 ? 300 : 0)),
        // The length guard lives inside the switchMap so that short/empty terms
        // still cancel any in-flight request instead of letting its late response
        // resurrect a cleared popup. null marks "cleared", distinct from a
        // completed search that happened to return zero results.
        switchMap((term) => (term.length >= 2 ? this.speciesSearchService.searchSpecies(term) : of(null))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => {
        if (results === null) {
          if (this.dismissed()) return;
          this.speciesResults.set([]);
          this.showAutocomplete.set(false);
          this.showNoResults.set(false);
          this.highlightedIndex.set(-1);
          return;
        }
        // Focus sitting on an option is destroyed when @for replaces the list.
        const hadOptionFocus =
          this.hostEl.nativeElement.ownerDocument.activeElement?.id.startsWith('species-option-') ?? false;
        if (this.dismissed()) {
          // Keep the payload fresh for an ArrowDown reopen, but stay closed.
          this.speciesResults.set(results);
          return;
        }
        this.speciesResults.set(results);
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
  }

  onSearchInput(event: Event): void {
    const detail = (event as CustomEvent<{ value: string }>).detail;
    const value = detail.value.trim();
    this.searchTerm.set(value);
    this.highlightedIndex.set(-1);
    this.dismissed.set(false);
    if (value.length < 2) {
      this.speciesResults.set([]);
      this.showAutocomplete.set(false);
      this.showNoResults.set(false);
    }
    this.searchInput$.next(value);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.showAutocomplete() || this.showNoResults()) {
        event.preventDefault();
        event.stopPropagation();
        this.dismissed.set(true);
        this.showAutocomplete.set(false);
        this.showNoResults.set(false);
        this.highlightedIndex.set(-1);
        this.focusSearchInput();
      }
      return;
    }
    const count = this.speciesResults().length;
    if (count === 0) return;
    if (!this.showAutocomplete()) {
      // APG combobox: ArrowDown reopens a popup dismissed with Escape when results remain.
      if (event.key === 'ArrowDown') {
        event.preventDefault();
        this.dismissed.set(false);
        this.showAutocomplete.set(true);
        this.showNoResults.set(false);
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

  private moveHighlight(update: (index: number) => number): void {
    this.highlightedIndex.update(update);
    // Signal writes only schedule change detection, so the option may not be in
    // the DOM yet (e.g. when ArrowDown reopens a dismissed popup). Focus after render.
    afterNextRender(
      () => {
        this.hostEl.nativeElement.querySelector<HTMLElement>(`#species-option-${this.highlightedIndex()}`)?.focus();
      },
      { injector: this.injector },
    );
  }

  private focusSearchInput(): void {
    const searchEl = this.searchField()?.nativeElement.querySelector('adb-search');
    const input = searchEl?.shadowRoot?.querySelector('input');
    if (input) {
      input.focus();
    } else {
      (searchEl as HTMLElement | null)?.focus?.();
    }
  }

  onSearchClear(): void {
    this.searchTerm.set('');
    this.speciesResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    this.highlightedIndex.set(-1);
    // Cancel any in-flight request and reset the distinctUntilChanged memory.
    this.searchInput$.next('');
  }

  onSearchSubmit(): void {
    // A popup dismissed with Escape must not resurrect its stale results on Enter.
    if (!this.showAutocomplete()) return;
    const results = this.speciesResults();
    if (results.length === 0) return;
    const index = this.highlightedIndex();
    this.onSpeciesSelect(index >= 0 && index < results.length ? results[index] : results[0]);
  }

  onSpeciesSelect(species: SpeciesDto): void {
    if (species.taxonId == null) return;
    this.filterState.addTaxon(species.taxonId);
    this.searchTerm.set('');
    this.speciesResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    this.highlightedIndex.set(-1);
    // Reset the search stream's distinctUntilChanged memory so retyping the
    // same term after this selection triggers a new request.
    this.searchInput$.next('');
    const searchEl = this.searchField()?.nativeElement.querySelector('adb-search') as (HTMLElement & { value: string }) | null;
    if (searchEl) searchEl.value = '';
    this.focusSearchInput();
  }

  getVernacularName(species: SpeciesDto): string {
    const names = species.preferredVernacularNames;
    if (!names || names.length === 0) return '';
    const nbName = names.find((n) => n.language === 'nb');
    return (nbName ?? names[0])?.name ?? '';
  }

  highlightMatch(text: string | null | undefined): string {
    if (!text) return '';
    const term = this.searchTerm();
    if (!term) return text;
    const words = term.split(/\s+/).filter((w) => w.length > 0);
    if (words.length === 0) return text;
    const escaped = words.map((w) => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
    const pattern = escaped.join('|');
    return text.replace(new RegExp(`(${pattern})`, 'gi'), '<strong>$1</strong>');
  }
}
