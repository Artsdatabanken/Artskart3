import { Injectable } from '@angular/core';
import { createNibToken, type NibTokenManager } from '@artsdatabanken/nbic-map-component';

@Injectable({
  providedIn: 'root'
})
export class SharedMapService {
  private nibTokenManager: NibTokenManager;

  constructor() {
    this.nibTokenManager = createNibToken({
      expiryMarginMinutes: 10,
      onError: (err) => console.error('NiB token fetch failed:', err.message)
    });

    this.nibTokenManager
      .init()
      .then(() => {
        console.log('NiB token Manager initialized, token cached');
      })
      .catch(() => {
        console.warn('NiB token: Initial token fetch failed, will retry automatically');
      });
  }

  getNibToken(): string {
    return this.nibTokenManager.getToken();
  }
}
