import {
  Component,
  ChangeDetectionStrategy,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
  signal,
  computed,
  DestroyRef,
  OnInit,
  ElementRef,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, switchMap, debounceTime, distinctUntilChanged, filter } from 'rxjs';
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
})
export class SpeciesSearchComponent implements OnInit {
  private readonly speciesSearchService = inject(SpeciesSearchService);
  private readonly filterState = inject(FilterStateService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly searchField = viewChild<ElementRef<HTMLElement>>('searchField');

  private readonly searchInput$ = new Subject<string>();
  readonly speciesResults = signal<SpeciesDto[]>([]);
  readonly showAutocomplete = signal(false);
  readonly showNoResults = signal(false);
  readonly searchTerm = signal('');
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
        debounceTime(300),
        distinctUntilChanged(),
        filter((term) => term.length >= 2),
        switchMap((term) => this.speciesSearchService.searchSpecies(term)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => {
        this.speciesResults.set(results);
        this.showAutocomplete.set(results.length > 0);
        this.showNoResults.set(results.length === 0);
      });
  }

  onSearchInput(event: Event): void {
    const detail = (event as CustomEvent<{ value: string }>).detail;
    const value = detail.value.trim();
    this.searchTerm.set(value);
    if (value.length < 2) {
      this.speciesResults.set([]);
      this.showAutocomplete.set(false);
      this.showNoResults.set(false);
    }
    this.searchInput$.next(value);
  }

  onSearchClear(): void {
    this.searchTerm.set('');
    this.speciesResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
  }

  onSearchSubmit(): void {
    const results = this.speciesResults();
    if (results.length > 0) {
      this.onSpeciesSelect(results[0]);
    }
  }

  onSpeciesSelect(species: SpeciesDto): void {
    if (species.taxonId == null) return;
    this.filterState.addTaxon(species.taxonId);
    this.searchTerm.set('');
    this.speciesResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    const searchEl = this.searchField()?.nativeElement.querySelector('adb-search') as (HTMLElement & { value: string }) | null;
    if (searchEl) searchEl.value = '';
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
