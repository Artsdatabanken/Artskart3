import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Service, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, retry, switchMap, throwError, timer } from 'rxjs';
import { CustomAreaLookupDto, ObservationSearchFilter } from '../../types/api.types';
import { FilterStateService } from '../filter-state/filter-state.service';
import { SearchFilterService } from '../search-filter/search-filter.service';

export type CustomAreaDrawingCommand = 'start' | 'finish' | 'undo' | 'cancel';

/** A polygon needs three corners. */
export const MIN_CUSTOM_AREA_POINTS = 3;

/**
 * The API's limit (SearchConstants.MaxCustomAreaVertices), counted the same way: every point in the
 * WKT, including the one that closes the ring. Drawing while holding Shift adds a point for every
 * movement, which easily goes past it.
 */
export const MAX_CUSTOM_AREA_POINTS = 1000;

/** Each reason is also the key of its message under sidebar.customArea. */
export type CustomAreaRejection = 'tooManyLocations' | 'tooManyLocationsWithAreas' | 'tooManyCorners' | 'checkFailed';

/** A drawn polygon the filter can't use. It stays on the map, so its corners can be dragged. */
export interface RejectedCustomArea {
  wkt: string;
  reason: CustomAreaRejection;
  /** The limit the message names, for the reasons that have one. */
  limit?: number;
}

/**
 * The area filters the API needs to check a polygon the way the searches use it: with any of
 * them selected, the polygon isn't split into the areas inside it and its edge.
 */
type AreaSelection = Pick<
  ObservationSearchFilter,
  'municipalityIds' | 'countyIds' | 'restrictedAreaIds' | 'oceanAreaIds' | 'cadastralUnitIds' | 'historicalAreaIds'
>;

interface SizeCheck {
  wkt: string;
  areas: AreaSelection;
  /** FilterStateService.customAreaVersion when the polygon was submitted. */
  version: number;
}

const RETRY_DELAY_MS = 1000;

const hasAreas = (areas: AreaSelection): boolean => Object.values(areas).some((ids) => ids !== undefined && ids !== null);

const pointCount = (wkt: string): number => wkt.split(',').length;

// The network, rate limiting and server errors can pass. A 400 means the API won't take the polygon.
const isTransient = (error: unknown): boolean =>
  error instanceof HttpErrorResponse && (error.status === 0 || error.status === 429 || error.status >= 500);

/**
 * Drawing a custom area: the sidebar has the buttons, the map does the drawing. A finished
 * polygon is checked by the API and goes into FilterStateService.customAreaWkt only when the
 * API says it's within the location limit. Otherwise it stays on the map as rejected.
 */
@Service()
export class CustomAreaDrawingService {
  private readonly http = inject(HttpClient);
  private readonly filterState = inject(FilterStateService);
  private readonly searchFilter = inject(SearchFilterService);

  private readonly commandSubject = new Subject<CustomAreaDrawingCommand>();
  readonly commands: Observable<CustomAreaDrawingCommand> = this.commandSubject.asObservable();

  private readonly drawing = signal(false);
  readonly isDrawing = this.drawing.asReadonly();

  private readonly points = signal(0);
  readonly canFinish = computed(() => this.drawing() && this.points() >= MIN_CUSTOM_AREA_POINTS);

  private readonly rejectedArea = signal<(RejectedCustomArea & { version: number }) | null>(null);
  /** A rejected polygon is gone once the filter's polygon is set or cleared from somewhere else. */
  readonly rejected = computed<RejectedCustomArea | null>(() => {
    const rejected = this.rejectedArea();
    if (!rejected || rejected.version !== this.filterState.customAreaVersion()) return null;
    return { wkt: rejected.wkt, reason: rejected.reason, limit: rejected.limit };
  });

  private readonly areaSelection = computed<AreaSelection>(
    () => {
      const filter = this.searchFilter.observationFilter();
      return {
        municipalityIds: filter.municipalityIds,
        countyIds: filter.countyIds,
        restrictedAreaIds: filter.restrictedAreaIds,
        oceanAreaIds: filter.oceanAreaIds,
        cadastralUnitIds: filter.cadastralUnitIds,
        historicalAreaIds: filter.historicalAreaIds,
      };
    },
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  private readonly sizeChecks = new Subject<SizeCheck>();
  /** The check whose answer hasn't arrived yet. */
  private pending?: SizeCheck;
  private checkedAreas?: string;
  /**
   * The latest FilterStateService.customAreaVersion this service has dealt with: its own changes to
   * the polygon, and the polygon it was given to check, whose answer replaces the one in the filter.
   */
  private ownVersion = 0;

  constructor() {
    this.sizeChecks
      .pipe(
        switchMap((check) =>
          this.http.post<CustomAreaLookupDto>('/api/Lookup/CustomArea', { wkt: check.wkt, filter: check.areas }).pipe(
            retry({
              count: 2,
              delay: (error) => (isTransient(error) ? timer(RETRY_DELAY_MS) : throwError(() => error)),
            }),
            map((result): CustomAreaLookupDto | null => result),
            catchError(() => of(null)),
            map((result) => ({ check, result })),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ check, result }) => {
        this.pending = undefined;
        // The filter's polygon was cleared or replaced while the check ran, so the answer is about
        // a polygon that isn't in use any more.
        if (check.version !== this.filterState.customAreaVersion()) return;

        if (!result) {
          // Refuses a new drawing, but keeps a polygon that's already in the filter, such as one from
          // a saved filter or one checked again after an area was added. Removing it would quietly
          // widen the search, and the searches enforce the limit themselves.
          if (check.wkt !== this.filterState.customAreaWkt()) this.reject(check.wkt, 'checkFailed');
        } else if (result.tooManyLocations) {
          this.reject(
            check.wkt,
            hasAreas(check.areas) ? 'tooManyLocationsWithAreas' : 'tooManyLocations',
            result.maxLocationCount ?? undefined,
          );
        } else {
          this.rejectedArea.set(null);
          this.filterState.setCustomArea(check.wkt);
          this.ownVersion = this.filterState.customAreaVersion();
        }
      });

    // A polygon put into the filter from elsewhere, such as a saved filter, was checked against the
    // data and the limit as they were when it was saved, so it is checked again like a drawn one.
    effect(() => {
      const version = this.filterState.customAreaVersion();
      untracked(() => {
        const wkt = this.filterState.customAreaWkt();
        if (version !== this.ownVersion && wkt) this.submit(wkt);
      });
    });

    // The other area filters decide how much of the polygon counts, so adding one can make the
    // polygon too large, and removing it again can make a rejected polygon fit. A check that's
    // still running is sent again with the new filters, since its answer would be about the old ones.
    effect(() => {
      const areas = JSON.stringify(this.areaSelection());
      untracked(() => {
        const changed = this.checkedAreas !== undefined && this.checkedAreas !== areas;
        this.checkedAreas = areas;
        if (!changed) return;

        if (this.pending) {
          this.send({ ...this.pending, areas: this.areaSelection() });
          return;
        }
        const wkt = this.rejected()?.wkt ?? this.filterState.customAreaWkt();
        if (wkt) this.submit(wkt);
      });
    });
  }

  start(): void {
    if (this.drawing()) return;
    this.drawing.set(true);
    this.points.set(0);
    this.commandSubject.next('start');
  }

  finish(): void {
    if (this.canFinish()) this.commandSubject.next('finish');
  }

  undo(): void {
    if (this.drawing()) this.commandSubject.next('undo');
  }

  /** Stops drawing without changing the filter. */
  cancel(): void {
    if (!this.drawing()) return;
    this.commandSubject.next('cancel');
    this.stopped();
  }

  /** Removes a rejected polygon from the map. */
  dismissRejected(): void {
    this.rejectedArea.set(null);
  }

  /** Called by the map when a polygon is finished or its corners are moved. */
  submit(wkt: string): void {
    if (pointCount(wkt) > MAX_CUSTOM_AREA_POINTS) {
      this.pending = undefined;
      // Clearing the filter's polygon also drops the answer to any check still running.
      this.reject(wkt, 'tooManyCorners', MAX_CUSTOM_AREA_POINTS);
      return;
    }
    this.ownVersion = this.filterState.customAreaVersion();
    this.send({ wkt, areas: this.areaSelection(), version: this.ownVersion });
  }

  /** Called by the map as corners are added and removed. */
  setPointCount(count: number): void {
    this.points.set(Math.max(0, count));
  }

  /** Called by the map when a polygon is finished, or drawing is cancelled. */
  stopped(): void {
    this.drawing.set(false);
    this.points.set(0);
  }

  private send(check: SizeCheck): void {
    this.pending = check;
    this.sizeChecks.next(check);
  }

  /** Takes the polygon out of the filter and keeps it on the map as rejected. */
  private reject(wkt: string, reason: CustomAreaRejection, limit?: number): void {
    this.filterState.clearCustomArea();
    this.ownVersion = this.filterState.customAreaVersion();
    this.rejectedArea.set({ wkt, reason, limit, version: this.ownVersion });
  }
}
