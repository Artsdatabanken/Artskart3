import { DOCUMENT, Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, ElementRef, inject, Injector, input, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, concat, map, merge, of, Subject, switchMap, takeUntil } from 'rxjs';
import { LoggingService } from '@shared/logging.service';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationDetailDto, ObservationListInfoDto, ObservationPointDto } from '@shared/types/api.types';
import { ObservationListComponent } from '../observation-list.component/observation-list.component';
import { ObservationRequestState, ObservationSelection } from '../observation-list.component/observation-list.model';
import { ObservationDetailsComponent, DetailState } from '../observation-details/observation-details.component';

@Component({
  imports: [ObservationListComponent, ObservationDetailsComponent],
  selector: 'app-observation-panel',
  styleUrl: './observation-panel.component.css',
  templateUrl: './observation-panel.component.html',
})
export class ObservationPanelComponent {
  readonly observations = input<ObservationListInfoDto[]>([]);
  readonly selection = input<ObservationSelection | null>(null);
  readonly requestState = input<ObservationRequestState>('ready');
  readonly retry = output<void>();
  readonly activePoint = output<ObservationPointDto | null>();
  readonly detailsOpened = output<void>();
  readonly listClosed = output<void>();
  readonly list = viewChild(ObservationListComponent);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly api = inject(ObservationService);
  private readonly logger = inject(LoggingService);
  private readonly retryDetail = new Subject<void>();
  private readonly dismissRequest = new Subject<void>();
  private readonly contextSeed = crypto.randomUUID();
  private readonly contextKey = computed(() => (this.selection() ? `${this.contextSeed}/${this.selection()!.key}` : null));
  private readonly activeContext = signal<string | null>(null);
  readonly rawId = signal<string | null>(null);
  readonly detailsOpen = computed(() => this.rawId() !== null);
  readonly selectedId = computed(() => {
    const value = this.rawId();
    return value && /^[1-9]\d*$/.test(value) && Number(value) <= 2147483647 ? Number(value) : null;
  });
  readonly state = signal<DetailState>('loading');
  readonly detail = signal<ObservationDetailDto | null>(null);
  readonly navigationError = signal(false);
  readonly orderedIds = computed(() =>
    this.activeContext() === this.contextKey() && this.activeContext() !== null ? (this.list()?.orderedObservationIds() ?? []) : [],
  );
  readonly position = computed(() => this.orderedIds().indexOf(this.selectedId() ?? -1));
  readonly count = computed(() => (this.position() >= 0 ? this.orderedIds().length : 1));
  private returnFocus: HTMLElement | null = null;

  constructor() {
    merge(this.route.queryParamMap.pipe(map(() => undefined)), this.retryDetail)
      .pipe(
        switchMap(() => {
          const raw = this.route.snapshot.queryParamMap.get('observationId');
          const history: unknown = this.location.getState();
          this.activeContext.set(
            history && typeof history === 'object' && 'observationContext' in history && typeof history.observationContext === 'string'
              ? history.observationContext
              : null,
          );
          const closing = this.rawId() !== null && raw === null;
          this.rawId.set(raw);
          this.detail.set(null);
          this.activePoint.emit(null);
          this.navigationError.set(false);
          if (raw === null) {
            if (closing) this.restoreFocus();
            return of({ state: 'loading' as const, detail: null });
          }
          this.detailsOpened.emit();
          const id = this.selectedId();
          if (id === null) {
            this.logger.warn('Invalid observation URL parameter', 'ObservationPanel', raw);
            return of({ state: 'notFound' as const, detail: null });
          }
          return concat(
            of({ state: 'loading' as const, detail: null }),
            this.api.getDetail(id).pipe(
              takeUntil(this.dismissRequest),
              map((detail) => ({ state: 'ready' as const, detail })),
              catchError((error: unknown) => {
                this.logger.error('Failed to load observation details', 'ObservationPanel', error);
                return of({
                  state: error instanceof HttpErrorResponse && error.status === 404 ? ('notFound' as const) : ('error' as const),
                  detail: null,
                });
              }),
            ),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(({ state, detail }) => {
        this.state.set(state);
        this.detail.set(detail);
        this.activePoint.emit(detail?.point ?? null);
      });
  }

  open(id: number): void {
    const active = this.document.activeElement;
    this.returnFocus =
      this.host.nativeElement.querySelector<HTMLElement>(`[data-observation-id="${id}"]`) ??
      (active instanceof HTMLElement ? active : null);
    this.navigate(id, false, this.contextKey());
  }

  move(direction: number): void {
    const ids = this.orderedIds();
    if (ids.length < 2 || this.position() < 0) return;
    this.navigate(ids[(this.position() + direction + ids.length) % ids.length], false, this.contextKey());
  }

  dismiss(invalidate = false): void {
    this.dismissRequest.next();
    this.detail.set(null);
    this.activePoint.emit(null);
    if (invalidate) {
      this.activeContext.set(null);
      this.returnFocus = null;
    }
    if (this.rawId() !== null) this.navigate(null, true, null);
  }

  closeList(): void {
    this.listClosed.emit();
    this.restoreFocus(null);
  }

  retryDetails(): void {
    this.retryDetail.next();
  }

  private navigate(id: number | null, replaceUrl: boolean, context: string | null): void {
    void this.router
      .navigate([], {
        relativeTo: this.route,
        queryParams: { observationId: id },
        queryParamsHandling: 'merge',
        replaceUrl,
        state: { observationContext: context },
      })
      .then((success) => {
        if (!success) this.failedNavigation(new Error('Observation navigation was cancelled'));
      })
      .catch((error: unknown) => this.failedNavigation(error));
  }

  private failedNavigation(error: unknown): void {
    this.logger.error('Failed to navigate to observation', 'ObservationPanel', error);
    this.navigationError.set(true);
    this.state.set('error');
  }

  private restoreFocus(target = this.returnFocus): void {
    afterNextRender(
      () => {
        if (target?.isConnected) target.focus({ preventScroll: true });
        else this.host.nativeElement.closest('.map-container')?.querySelector<HTMLElement>('button')?.focus();
      },
      { injector: this.injector },
    );
  }
}
