import { HttpErrorResponse } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import { LoggingService } from '@shared/logging.service';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationDetailDto } from '@shared/types/api.types';
import { DetailState } from './observation-details.component';

export interface DetailLoad {
  readonly state: DetailState;
  readonly detail: ObservationDetailDto | null;
}

export function loadObservationDetail(
  api: ObservationService,
  logger: LoggingService,
  id: number,
  context: string,
): Observable<DetailLoad> {
  return api.getDetail(id).pipe(
    map((detail) => ({ state: 'ready' as const, detail })),
    catchError((error: unknown) => of(detailLoadFailed(error, logger, context))),
  );
}

export function detailLoadFailed(error: unknown, logger: LoggingService, context: string): DetailLoad {
  // Links from other apps can point to observations that no longer exist, so a 404 is expected.
  if (error instanceof HttpErrorResponse && error.status === 404) {
    logger.warn('Observation not found', context, error.url);
    return { state: 'notFound', detail: null };
  }
  logger.error('Failed to load observation details', context, error);
  return { state: 'error', detail: null };
}
