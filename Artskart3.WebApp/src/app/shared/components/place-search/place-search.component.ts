import {
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  DestroyRef,
  ElementRef,
  OnInit,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, of, timer, switchMap, debounce, distinctUntilChanged, catchError } from 'rxjs';
import { PlaceSearchService } from '../../services/place-search/place-search.service';
import { LoggingService } from '@shared/logging.service';
import { PlaceSearchResultDto } from '../../types/api.types';

@Component({
  selector: 'app-place-search',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './place-search.component.html',
  styleUrl: './place-search.component.css',
})
export class PlaceSearchComponent implements OnInit {
  private readonly placeSearchService = inject(PlaceSearchService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly logger = inject(LoggingService);
  private readonly hostEl = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly placeSelected = output<PlaceSearchResultDto>();

  private readonly searchInput$ = new Subject<string>();
  readonly placeResults = signal<PlaceSearchResultDto[]>([]);
  readonly showAutocomplete = signal(false);
  readonly showNoResults = signal(false);
  readonly searchTerm = signal('');

  ngOnInit(): void {
    this.searchInput$
      .pipe(
        distinctUntilChanged(),
        debounce((term) => timer(term.length >= 2 ? 300 : 0)),
        switchMap((term) =>
          term.length >= 2
            ? this.placeSearchService.searchPlaces(term).pipe(
                catchError((err: unknown) => {
                  this.logger.error('Failed to search places', 'PlaceSearchComponent', err);
                  return of([]);
                }),
              )
            : of(null),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => {
        if (results === null) {
          this.placeResults.set([]);
          this.showAutocomplete.set(false);
          this.showNoResults.set(false);
          return;
        }
        this.placeResults.set(results);
        this.showAutocomplete.set(results.length > 0);
        this.showNoResults.set(results.length === 0);
      });
  }

  onSearchInput(event: Event): void {
    const detail = (event as CustomEvent<{ value: string }>).detail;
    const value = detail.value.trim();
    this.searchTerm.set(value);
    if (value.length < 2) {
      this.placeResults.set([]);
      this.showAutocomplete.set(false);
      this.showNoResults.set(false);
    }
    this.searchInput$.next(value);
  }

  onSearchClear(): void {
    this.searchTerm.set('');
    this.placeResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    this.searchInput$.next('');
  }

  onPlaceSelect(place: PlaceSearchResultDto): void {
    this.placeSelected.emit(place);
    this.dismiss();
  }

  private dismiss(): void {
    this.placeResults.set([]);
    this.showAutocomplete.set(false);
    this.showNoResults.set(false);
    const searchEl = this.hostEl.nativeElement.querySelector('adb-search') as (HTMLElement & { value: string }) | null;
    if (searchEl) searchEl.value = '';
    this.searchTerm.set('');
    this.searchInput$.next('');
  }
}
