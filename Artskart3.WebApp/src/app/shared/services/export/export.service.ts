import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subscription, timer, switchMap, takeWhile, last } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { CsvExportJobDto, CSV_EXPORT_STATUS, ObservationSearchFilter, StartExportRequestDto } from '../../types/api.types';
import { AlertService } from '../alert/alert.service';

@Injectable({
  providedIn: 'root',
})
export class ExportService {
  private readonly http = inject(HttpClient);
  private readonly alertService = inject(AlertService);
  private readonly translate = inject(TranslateService);
  private readonly baseUrl = '/api/export/csv';
  private readonly activePolls = new Map<number, Subscription>();

  /** Incremented when an export completes — use as a dependency to trigger refetch. */
  readonly historyVersion = signal(0);

  startExport(filter: ObservationSearchFilter): Observable<{ jobId: number }> {
    const request: StartExportRequestDto = {
      filter,
      selectedColumns: [],
    };
    return this.http.post<{ jobId: number }>(`${this.baseUrl}/start`, request);
  }

  getStatus(jobId: number): Observable<CsvExportJobDto> {
    return this.http.get<CsvExportJobDto>(`${this.baseUrl}/${jobId}/status`);
  }

  /**
   * Polls the export status every `intervalMs` until it completes or fails.
   * Emits intermediate statuses and completes with the final one.
   */
  pollUntilDone(jobId: number, intervalMs = 5000): Observable<CsvExportJobDto> {
    return timer(0, intervalMs).pipe(
      switchMap(() => this.getStatus(jobId)),
      takeWhile(
        (job) => job.status === CSV_EXPORT_STATUS.Pending || job.status === CSV_EXPORT_STATUS.Processing,
        true,
      ),
      last(),
    );
  }

  /**
   * Starts polling for an export job and shows alerts on completion/failure.
   * Survives navigation since ExportService is root-scoped.
   */
  trackExport(jobId: number): void {
    if (this.activePolls.has(jobId)) return;

    const sub = this.pollUntilDone(jobId).subscribe({
      next: (job) => {
        this.activePolls.delete(jobId);
        if (job.status === CSV_EXPORT_STATUS.Complete) {
          this.alertService.showSuccess(this.translate.instant('export.complete'), { autoDismissMs: 10000 });
        } else {
          this.alertService.showError(this.translate.instant('export.failed'));
        }
        this.historyVersion.update((v) => v + 1);
      },
      error: () => {
        this.activePolls.delete(jobId);
        this.alertService.showError(this.translate.instant('export.statusCheckFailed'));
      },
    });

    this.activePolls.set(jobId, sub);
  }

  getHistory(): Observable<CsvExportJobDto[]> {
    return this.http.get<CsvExportJobDto[]>(`${this.baseUrl}/history`);
  }

  getDownloadUrl(jobId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/${jobId}/download`);
  }

  getExcelDownloadUrl(jobId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/${jobId}/download/excel`);
  }
}
