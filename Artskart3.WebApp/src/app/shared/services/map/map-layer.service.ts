import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, of, startWith, switchMap, tap } from 'rxjs';
import { ApiClientService } from '@core/services/api-client.service';
import { MapLayerDto } from '../../types/api.types';
import { AlertService } from '../alert/alert.service';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class MapLayerService {
  private readonly apiClient = inject(ApiClientService);
  private readonly alerts = inject(AlertService);
  private readonly translate = inject(TranslateService);
  private readonly failedLayerIdsState = signal<ReadonlySet<number>>(new Set());
  readonly failedLayerIds = this.failedLayerIdsState.asReadonly();
  private layerLoadWarningId: number | null = null;
  private readonly reloadMapLayers$ = new Subject<void>();
  private readonly mapLayersLoadFailedState = signal(false);
  readonly mapLayersLoadFailed = this.mapLayersLoadFailedState.asReadonly();

  readonly mapLayers = toSignal(
    this.reloadMapLayers$.pipe(
      startWith(undefined),
      switchMap(() =>
        this.getMapLayers().pipe(
          tap(() => this.mapLayersLoadFailedState.set(false)),
          catchError(() => {
            this.mapLayersLoadFailedState.set(true);
            return of([] as MapLayerDto[]);
          }),
        ),
      ),
    ),
    { initialValue: [] as MapLayerDto[] },
  );
  private readonly enabledLayerIdsState = signal<ReadonlySet<number>>(new Set());
  readonly enabledLayerIds = this.enabledLayerIdsState.asReadonly();
  /** De påslåtte lagene i samme rekkefølge som kartlagslisten. */
  readonly activeLayers = computed(() => this.mapLayers().filter((layer) => this.enabledLayerIds().has(layer.id)));

  setLayerEnabled(layerId: number, enabled: boolean): void {
    const current = this.enabledLayerIdsState();
    if (current.has(layerId) === enabled) return;

    const next = new Set(current);
    if (enabled) next.add(layerId);
    else next.delete(layerId);
    this.enabledLayerIdsState.set(next);
  }

  setLayerFailed(layerId: number, failed: boolean): void {
    const current = this.failedLayerIdsState();
    if (current.has(layerId) === failed) return;

    const next = new Set(current);
    if (failed) next.add(layerId);
    else next.delete(layerId);
    this.failedLayerIdsState.set(next);

    const hadFailures = current.size > 0;
    const hasFailures = next.size > 0;

    if (!hadFailures && hasFailures) {
      this.layerLoadWarningId = this.alerts.showWarning(this.translate.instant('mapToolbar.layersLoadError'), { autoDismissMs: 0 });
    } else if (hadFailures && !hasFailures && this.layerLoadWarningId !== null) {
      this.alerts.dismiss(this.layerLoadWarningId);
      this.layerLoadWarningId = null;
    }
  }

  getMapLayers(): Observable<MapLayerDto[]> {
    return this.apiClient.fetchJson<MapLayerDto[]>('/api/MapLayer');
  }

  retryMapLayersLoad(): void {
    if (this.mapLayersLoadFailed()) this.reloadMapLayers$.next();
  }
}
