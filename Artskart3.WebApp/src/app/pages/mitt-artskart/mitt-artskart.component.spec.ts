import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MittArtskartComponent } from './mitt-artskart.component';
import { SavedFiltersTableComponent } from './saved-filters-table/saved-filters-table.component';
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
  let initialSessionRequest: TestRequest;
  let sessionResolved: boolean;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MittArtskartComponent, TranslateModule.forRoot()],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
      // Tabellen for lagrede filtre har egne tester og egne HTTP-kall.
      .overrideComponent(MittArtskartComponent, { remove: { imports: [SavedFiltersTableComponent] } })
      .compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('no', TRANSLATIONS);
    translate.use('no');

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MittArtskartComponent);
    component = fixture.componentInstance;
    sessionResolved = false;
    initialSessionRequest = httpTesting.expectOne('bff/user');
  });

  afterEach(() => {
    fixture.destroy();
    if (!sessionResolved) {
      sessionResolved = true;
      initialSessionRequest.flush(null);
    }
    httpTesting.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('waits for the BFF session before rendering guest controls and tabs', () => {
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.status-message')).not.toBeNull();
    expect(element.querySelector('.log-in')).toBeNull();
    expect(element.querySelector('.temporary-export-message')).toBeNull();
    expect(element.querySelector('adb-tabs')).toBeNull();
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

  it('should mark complete jobs as downloadable', () => {
    const job = { status: CSV_EXPORT_STATUS.Complete } as CsvExportJobDto;
    expect(component.isDownloadable(job)).toBe(true);
  });

  it('should not mark pending jobs as downloadable', () => {
    const job = { status: CSV_EXPORT_STATUS.Pending } as CsvExportJobDto;
    expect(component.isDownloadable(job)).toBe(false);
  });
});
