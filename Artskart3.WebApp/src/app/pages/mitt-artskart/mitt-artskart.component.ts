import { Component, ChangeDetectionStrategy, inject, CUSTOM_ELEMENTS_SCHEMA, effect, untracked, OnDestroy } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ExportService } from '../../shared/services/export/export.service';
import { AlertService } from '../../shared/services/alert/alert.service';
import { CsvExportJobDto, CSV_EXPORT_STATUS } from '../../shared/types/api.types';
import { LocaleDateTimePipe } from '../../shared/pipes/locale-date-time.pipe';
import { FormatFileSizePipe } from '../../shared/pipes/format-file-size.pipe';

@Component({
  selector: 'app-mitt-artskart',
  imports: [LocaleDateTimePipe, FormatFileSizePipe, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mitt-artskart.component.html',
  styleUrl: './mitt-artskart.component.css',
})
export class MittArtskartComponent implements OnDestroy {
  private readonly exportService = inject(ExportService);
  protected readonly translate = inject(TranslateService);
  private readonly alertService = inject(AlertService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly historyResource = rxResource<CsvExportJobDto[], void>({
    stream: () => this.exportService.getHistory(),
  });

  constructor() {
    effect(() => {
      this.exportService.historyVersion();
      untracked(() => this.historyResource.reload());
    });

    effect(() => {
      const jobs = this.historyResource.value() ?? [];
      const hasActiveJobs = jobs.some((j) => j.status === CSV_EXPORT_STATUS.Pending || j.status === CSV_EXPORT_STATUS.Processing);

      if (hasActiveJobs && !this.pollTimer) {
        this.pollTimer = setInterval(() => this.historyResource.reload(), 1000);
      } else if (!hasActiveJobs && this.pollTimer) {
        clearInterval(this.pollTimer);
        this.pollTimer = null;
      }
    });
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  getExportName(job: CsvExportJobDto): string {
    const baseName = job.name ?? this.translate.instant('mittArtskart.exportBaseName', { id: job.id });
    if (job.status === CSV_EXPORT_STATUS.Processing && (job.totalRows ?? 0) > 0) {
      // Note: maxes out at "95%" as it takes few seconds from the file is finished procesing and it is uploaded to blob storage
      const percent = Math.min(Math.round(((job.rowsProcessed ?? 0) / job.totalRows!) * 100), 95);
      return this.translate.instant('mittArtskart.exportProcessing', { percent, name: baseName });
    }
    if (job.status === CSV_EXPORT_STATUS.Pending) {
      return this.translate.instant('mittArtskart.exportPending', { name: baseName });
    }
    return baseName;
  }

  getFileName(job: CsvExportJobDto, extension: string): string {
    return `${job.fileName}.${extension}`;
  }

  isDownloadable(job: CsvExportJobDto): boolean {
    return job.status === CSV_EXPORT_STATUS.Complete;
  }

  isFailed(job: CsvExportJobDto): boolean {
    return job.status === CSV_EXPORT_STATUS.Failed;
  }

  onDownload(job: CsvExportJobDto): void {
    if (!job.id) return;
    this.exportService.downloadFile(job.id).subscribe({
      next: (blob) => this.triggerDownload(blob, this.getFileName(job, 'csv')),
      error: () => this.alertService.showError(this.translate.instant('mittArtskart.downloadFailed')),
    });
  }

  onDownloadExcel(job: CsvExportJobDto): void {
    if (!job.id) return;
    this.exportService.downloadExcelFile(job.id).subscribe({
      next: (blob) => this.triggerDownload(blob, this.getFileName(job, 'xlsx')),
      error: () => this.alertService.showError(this.translate.instant('mittArtskart.downloadFailed')),
    });
  }

  private triggerDownload(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}
