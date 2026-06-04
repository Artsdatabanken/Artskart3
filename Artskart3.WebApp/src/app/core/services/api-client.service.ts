/**
 * API Client Service
 * Centralized HTTP client for API interactions with type safety and error handling
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retryWhen, delay, take } from 'rxjs/operators';
import { environment } from '@env/environment';
import { ApiMessages, RetryConfig } from '@core/constants/api-messages';
import { LoggingService } from '@shared/logging.service';

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {
  private static readonly SERVICE_NAME = 'ApiClientService';

  private readonly http = inject(HttpClient);
  private readonly logger = inject(LoggingService);
  private readonly apiUrl = environment.apiUrl;

  fetchJson<T>(endpoint: string, options?: { responseType?: 'json' | 'text' }): Observable<T> {
    const url = this.buildUrl(endpoint);
    const responseType = options?.responseType ?? 'json';

    const request$ = responseType === 'text'
      ? (this.http.get(url, { responseType: 'text' }) as Observable<T>)
      : this.http.get<T>(url);

    return request$.pipe(
      retryWhen(errors =>
        errors.pipe(
          delay(RetryConfig.InitialDelayMs),
          take(RetryConfig.MaxAttempts - 1)
        )
      ),
      catchError(error => this.handleError(error, `Failed to fetch ${endpoint}`))
    );
  }

  parseJsonResponse<T>(responseText: string, context?: string): T {
    if (!responseText?.trim()) {
      const msg = `Empty response from ${context || 'API'}`;
      this.logger.warn(msg, ApiClientService.SERVICE_NAME);
      throw new Error(msg);
    }

    try {
      return JSON.parse(responseText) as T;
    } catch (error) {
      this.logger.error(ApiMessages.Errors.GeoJsonParseError, ApiClientService.SERVICE_NAME, error);
      throw new Error(ApiMessages.Errors.GeoJsonParseError, { cause: error });
    }
  }

  private buildUrl(endpoint: string): string {
    if (endpoint.startsWith('http')) {
      return endpoint;
    }

    if (!this.apiUrl?.trim()) {
      return endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
    }

    const separator = endpoint.startsWith('/') ? '' : '/';
    return `${this.apiUrl}${separator}${endpoint}`;
  }

  private handleError(error: HttpErrorResponse | Error, context: string): Observable<never> {
    let errorMessage = context;

    if (error instanceof HttpErrorResponse) {
      errorMessage = this.mapHttpError(error.status, error);
    } else if (error instanceof Error) {
      errorMessage = error.message;
    }

    this.logger.error(errorMessage, ApiClientService.SERVICE_NAME, error);
    return throwError(() => new Error(errorMessage, { cause: error }));
  }

  private mapHttpError(status: number, error: HttpErrorResponse): string {
    const errorMap: Record<number, string> = {
      0: ApiMessages.Errors.NetworkError,
      401: ApiMessages.Errors.Unauthorized,
      403: ApiMessages.Errors.Forbidden,
      503: ApiMessages.Errors.ApiUnavailable
    };

    return errorMap[status] ?? error.error?.error ?? error.message ?? 'Unknown error';
  }
}
