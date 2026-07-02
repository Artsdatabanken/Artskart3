import { Injectable, inject } from '@angular/core';
import { createNibToken, type NibTokenManager } from '@artsdatabanken/nbic-map-component';
import { LoggingService } from '@shared/logging.service';

@Injectable({
  providedIn: 'root'
})
export class SharedMapService {
  private static readonly SERVICE_NAME = 'SharedMapService';
  private readonly logger: LoggingService = inject(LoggingService);
  private nibTokenManager: NibTokenManager;

  constructor() {
    this.nibTokenManager = createNibToken({
      expiryMarginMinutes: 10,
      onError: (err: unknown) => this.logger.error('NiB token fetch failed', SharedMapService.SERVICE_NAME, err)
    });

    this.nibTokenManager.init().catch((error: unknown) => {
      this.logger.warn('NiB token: Initial fetch failed, will retry automatically', SharedMapService.SERVICE_NAME, error);
    });
  }

  getNibToken(): string {
    return this.nibTokenManager.getToken() || '';
  }
}
