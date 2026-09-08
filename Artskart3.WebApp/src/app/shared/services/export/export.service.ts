import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subscription, timer, switchMap, takeWhile, last } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { CsvExportJobDto, CSV_EXPORT_STATUS, ExportSummaryDto, ObservationSearchFilter, StartExportRequestDto } from '../../types/api.types';
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

  getSummary(filter: ObservationSearchFilter, name: string): Observable<ExportSummaryDto> {
    const request: StartExportRequestDto = {
      name,
      filter,
      selectedColumns: [],
    };
    return this.http.post<ExportSummaryDto>(`${this.baseUrl}/summary`, request);
  }

  startExport(filter: ObservationSearchFilter, name: string): Observable<{ jobId: number }> {
    const request: StartExportRequestDto = {
      name,
      filter,
      selectedColumns: [],
    };
    return this.http.post<{ jobId: number }>(`${this.baseUrl}/start`, request);
  }

  getStatus(jobId: number): Observable<CsvExportJobDto> {
    return this.http.get<CsvExportJobDto>(`${this.baseUrl}/${jobId}/status`);
  }

  /**
   * Avbryter en eksportjobb som ennå ikke er ferdig.
   *
   * Endepunktet har eksistert hele tiden, men var aldri koblet opp i klienten.
   * Uten det var grensen på tre samtidige jobber en blindvei: tre jobber som ble
   * stående låste brukeren ute fra all eksport, med «prøv igjen senere» som
   * eneste tilbakemelding og en manuell databaseoppdatering som eneste utvei.
   */
  cancelExport(jobId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${jobId}/cancel`, {});
  }

  /** Stopper statuspollingen for en jobb, f.eks. når den er avbrutt. */
  stopTracking(jobId: number): void {
    this.activePolls.get(jobId)?.unsubscribe();
    this.activePolls.delete(jobId);
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
          this.alertService.showSuccess('', {
            heading: this.translate.instant('export.complete'),
            autoDismissMs: 10000,
            link: {
              text: this.translate.instant('export.goToExport'),
              route: '/mittartskart',
            },
          });
        } else if (job.status !== CSV_EXPORT_STATUS.Cancelled) {
          // Avbrutt er brukerens eget valg, ikke en feil — ingen toast.
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

  downloadFile(jobId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${jobId}/download`, { responseType: 'blob' });
  }

  downloadExcelFile(jobId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${jobId}/download/excel`, { responseType: 'blob' });
  }
}
