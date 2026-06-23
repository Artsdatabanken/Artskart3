import {
  Component,
  ChangeDetectionStrategy,
  inject,
  CUSTOM_ELEMENTS_SCHEMA,
  effect,
  OnDestroy,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ExportService } from '../../shared/services/export/export.service';
import { AlertService } from '../../shared/services/alert/alert.service';
import { LanguageService } from '../../shared/services/languages/language.service';
import { CsvExportJobDto, CSV_EXPORT_STATUS } from '../../shared/types/api.types';
import { LocaleDateTimePipe } from '../../shared/pipes/locale-date-time.pipe';

@Component({
  selector: 'app-mitt-artskart',
  imports: [LocaleDateTimePipe, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mitt-artskart.component.html',
  styleUrl: './mitt-artskart.component.css',
})
export class MittArtskartComponent implements OnDestroy {
  private readonly exportService = inject(ExportService);
  private readonly translate = inject(TranslateService);
  private readonly alertService = inject(AlertService);
  private readonly languageService = inject(LanguageService);
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly historyResource = rxResource<CsvExportJobDto[], void>({
    stream: () => this.exportService.getHistory(),
  });

  constructor() {
    effect(() => {
      this.exportService.historyVersion();
      this.historyResource.reload();
    });

    effect(() => {
      const jobs = this.historyResource.value() ?? [];
      const hasActiveJobs = jobs.some(
        (j) => j.status === CSV_EXPORT_STATUS.Pending || j.status === CSV_EXPORT_STATUS.Processing,
      );

      if (hasActiveJobs && !this.pollTimer) {
        this.pollTimer = setInterval(() => this.historyResource.reload(), 5000);
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
    const baseName = this.translate.instant('mittArtskart.exportBaseName', { id: job.id });
    if (job.status === CSV_EXPORT_STATUS.Processing && (job.totalRows ?? 0) > 0) {
      const percent = Math.round(((job.rowsProcessed ?? 0) / job.totalRows!) * 100);
      return this.translate.instant('mittArtskart.exportProcessing', { percent, name: baseName });
    }
    if (job.status === CSV_EXPORT_STATUS.Pending) {
      return this.translate.instant('mittArtskart.exportPending', { name: baseName });
    }
    return baseName;
  }

  getFileSize(job: CsvExportJobDto): string {
    if (!job.fileSize) return '-';
    const bytes = job.fileSize;
    const { unit, value } =
      bytes >= 1024 ** 3
        ? { unit: 'gigabyte', value: bytes / 1024 ** 3 }
        : bytes >= 1024 ** 2
          ? { unit: 'megabyte', value: bytes / 1024 ** 2 }
          : bytes >= 1024
            ? { unit: 'kilobyte', value: bytes / 1024 }
            : { unit: 'byte', value: bytes };
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'unit',
      unit,
      maximumFractionDigits: unit === 'byte' ? 0 : 1,
    }).format(value);
  }

  private getLocale(): string {
    return this.languageService.getLanguage() === 'no' ? 'nb-NO' : 'en';
  }

  isDownloadable(job: CsvExportJobDto): boolean {
    return job.status === CSV_EXPORT_STATUS.Complete;
  }

  onDownload(job: CsvExportJobDto): void {
    if (!job.id) return;
    const newWindow = window.open('', '_blank', 'noopener,noreferrer');
    this.exportService.getDownloadUrl(job.id).subscribe({
      next: (response) => {
        if (newWindow) {
          newWindow.location.href = response.url;
        } else {
          window.location.href = response.url;
        }
      },
      error: () => {
        if (newWindow) newWindow.close();
        this.alertService.showError(this.translate.instant('mittArtskart.downloadFailed'));
      },
    });
  }

  onDownloadExcel(job: CsvExportJobDto): void {
    if (!job.id) return;
    const newWindow = window.open('', '_blank', 'noopener,noreferrer');
    this.exportService.getExcelDownloadUrl(job.id).subscribe({
      next: (response) => {
        if (newWindow) {
          newWindow.location.href = response.url;
        } else {
          window.location.href = response.url;
        }
      },
      error: () => {
        if (newWindow) newWindow.close();
        this.alertService.showError(this.translate.instant('mittArtskart.downloadFailed'));
      },
    });
  }
}
