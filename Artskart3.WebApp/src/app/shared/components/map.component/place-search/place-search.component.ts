import { Component, CUSTOM_ELEMENTS_SCHEMA, ElementRef, inject, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, catchError, distinctUntilChanged, finalize, map, of, switchMap, timer } from 'rxjs';
import { PlaceSearchResult } from '../../../types/place-search.types';
import { PlaceSearchService } from '../../../services/place-search/place-search.service';

type SearchState =
  | { kind: 'clear' }
  | { kind: 'results'; results: PlaceSearchResult[] }
  | { kind: 'error' };

@Component({
  selector: 'app-place-search',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './place-search.component.html',
  styleUrl: './place-search.component.css',
  host: {
    '(keydown)': 'onKeydown($event)',
  },
})
export class PlaceSearchComponent {
  private readonly searchService = inject(PlaceSearchService);
  private readonly searchField = viewChild<ElementRef<HTMLElement>>('searchField');
  private readonly hostElement = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly searchInput$ = new Subject<{ term: string; immediate: boolean }>();

  readonly placeSelected = output<PlaceSearchResult>();
  readonly results = signal<PlaceSearchResult[]>([]);
  readonly showResults = signal(false);
  readonly showNoResults = signal(false);
  readonly isLoading = signal(false);
  readonly hasError = signal(false);
  readonly searchTerm = signal('');
  readonly highlightedIndex = signal(-1);
  constructor() {
    this.searchInput$
      .pipe(
        distinctUntilChanged((previous, current) => previous.term === current.term && !current.immediate),
        switchMap(({ term, immediate }) => {
          if (term.length < 2) return of<SearchState>({ kind: 'clear' });

          return timer(immediate ? 0 : 250).pipe(
            switchMap(() => {
              this.isLoading.set(true);
              this.showNoResults.set(false);
              this.hasError.set(false);
              return this.searchService.searchPlaces(term).pipe(
                map((results): SearchState => ({ kind: 'results', results })),
                catchError(() => of<SearchState>({ kind: 'error' })),
                finalize(() => this.isLoading.set(false)),
              );
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((state) => {
        if (state.kind === 'clear') {
          this.results.set([]);
          this.showResults.set(false);
          this.showNoResults.set(false);
          this.hasError.set(false);
          this.isLoading.set(false);
          this.highlightedIndex.set(-1);
          return;
        }

        if (state.kind === 'error') {
          this.results.set([]);
          this.showResults.set(false);
          this.showNoResults.set(false);
          this.hasError.set(true);
          this.highlightedIndex.set(-1);
          return;
        }

        this.results.set(state.results);
        this.showResults.set(state.results.length > 0);
        this.showNoResults.set(state.results.length === 0);
        this.hasError.set(false);
        this.highlightedIndex.set(-1);
      });
  }

  onSearchInput(event: Event): void {
    const detail = (event as CustomEvent<{ value: string }>).detail;
    const value = detail?.value?.trim() ?? '';
    this.searchTerm.set(value);
    this.highlightedIndex.set(-1);
    if (value.length < 2) {
      this.results.set([]);
      this.showResults.set(false);
      this.showNoResults.set(false);
      this.hasError.set(false);
      this.isLoading.set(false);
    }
    this.searchInput$.next({ term: value, immediate: false });
  }

  onSearchClear(): void {
    this.searchTerm.set('');
    this.searchInput$.next({ term: '', immediate: true });
    this.results.set([]);
    this.showResults.set(false);
    this.showNoResults.set(false);
    this.hasError.set(false);
    this.isLoading.set(false);
    this.highlightedIndex.set(-1);
  }

  onSearchSubmit(): void {
    if (!this.results().length) {
      const term = this.searchTerm();
      if (term.length >= 2) this.searchInput$.next({ term, immediate: true });
      return;
    }
    const index = this.highlightedIndex();
    this.onPlaceSelect(this.results()[index >= 0 ? index : 0]);
  }

  onKeydown(event: KeyboardEvent): void {
    const resultCount = this.results().length;
    if (event.key === 'Escape') {
      if (this.showResults() || this.showNoResults() || this.hasError()) {
        event.preventDefault();
        this.showResults.set(false);
        this.showNoResults.set(false);
        this.hasError.set(false);
        this.highlightedIndex.set(-1);
        this.focusSearchInput();
      }
      return;
    }
    if (!this.showResults() || resultCount === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.moveHighlight((index) => (index + 1) % resultCount);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.moveHighlight((index) => (index <= 0 ? resultCount - 1 : index - 1));
    }
  }

  onPlaceSelect(place: PlaceSearchResult): void {
    this.placeSelected.emit(place);
    this.onSearchClear();
    this.focusSearchInput();
  }

  getLocationLabel(place: PlaceSearchResult): string {
    return [...place.municipalities, ...place.counties].join(', ');
  }

  private moveHighlight(update: (index: number) => number): void {
    this.highlightedIndex.update(update);
    requestAnimationFrame(() => {
      this.hostElement.nativeElement.querySelector<HTMLElement>(`#place-option-${this.highlightedIndex()}`)?.focus();
    });
  }

  private focusSearchInput(): void {
    const searchElement = this.searchField()?.nativeElement.querySelector('adb-search');
    const input = searchElement?.shadowRoot?.querySelector('input');
    if (input) {
      input.focus();
    } else {
      (searchElement as HTMLElement | null)?.focus?.();
    }
  }
}