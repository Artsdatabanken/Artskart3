import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MittArtskartComponent } from './mitt-artskart.component';
import { CsvExportJobDto, CSV_EXPORT_STATUS } from '../../shared/types/api.types';

const TRANSLATIONS = {
  mittArtskart: {
    exportBaseName: 'Eksport #{{id}}',
    exportProcessing: 'Eksporterer ({{percent}}%) - {{name}}',
    exportPending: 'Venter - {{name}}',
  },
};

describe('MittArtskartComponent', () => {
  let component: MittArtskartComponent;
  let fixture: ComponentFixture<MittArtskartComponent>;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MittArtskartComponent, TranslateModule.forRoot()],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('no', TRANSLATIONS);
    translate.use('no');

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MittArtskartComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display export name with progress for processing jobs', () => {
    const job: CsvExportJobDto = {
      id: 42,
      status: CSV_EXPORT_STATUS.Processing,
      totalRows: 100,
      rowsProcessed: 3,
      fileSize: 0,
      createdAt: '2026-01-01T00:00:00Z',
      startedAt: '2026-01-01T00:00:01Z',
      completedAt: null,
      expiresAt: null,
      errorMessage: null,
    };
    expect(component.getExportName(job)).toBe('Eksporterer (3%) - Eksport #42');
  });

  it('should display plain name for complete jobs', () => {
    const job: CsvExportJobDto = {
      id: 7,
      status: CSV_EXPORT_STATUS.Complete,
      totalRows: 500,
      rowsProcessed: 500,
      fileSize: 597000,
      createdAt: '2026-01-01T00:00:00Z',
      startedAt: '2026-01-01T00:00:01Z',
      completedAt: '2026-01-01T00:01:00Z',
      expiresAt: null,
      errorMessage: null,
    };
    expect(component.getExportName(job)).toBe('Eksport #7');
  });

  it('should format file size in kB', () => {
    const job = { fileSize: 597504 } as CsvExportJobDto;
    expect(component.getFileSize(job)).toBe('583,5 kB');
  });

  it('should format file size in MB for large files', () => {
    const job = { fileSize: 10485760 } as CsvExportJobDto;
    expect(component.getFileSize(job)).toBe('10 MB');
  });

  it('should return "-" for missing file size', () => {
    const job = { fileSize: 0 } as CsvExportJobDto;
    expect(component.getFileSize(job)).toBe('-');
  });

  it('should mark complete jobs as downloadable', () => {
    const job = { status: CSV_EXPORT_STATUS.Complete } as CsvExportJobDto;
    expect(component.isDownloadable(job)).toBe(true);
  });

  it('should not mark pending jobs as downloadable', () => {
    const job = { status: CSV_EXPORT_STATUS.Pending } as CsvExportJobDto;
    expect(component.isDownloadable(job)).toBe(false);
  });
});
