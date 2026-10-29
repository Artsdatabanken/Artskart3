import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, of } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { ExportService } from './export.service';
import { anonymousUserInterceptor } from '../../interceptors/anonymous-user.interceptor';
import { AuthService, Session } from '../auth/auth.service';
import { AlertService } from '../alert/alert.service';

const STORAGE_KEY = 'artskart.anonymousUserId';
const ANONYMOUS_USER_ID = '95e445a5-caa6-4b54-ae93-22d31ecc6b01';
const AUTHENTICATED_SESSION: Session = [{ type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' }];

describe('ExportService', () => {
  let service: ExportService;
  let httpTesting: HttpTestingController;
  let session: Session;

  beforeEach(() => {
    session = AUTHENTICATED_SESSION;
    localStorage.setItem(STORAGE_KEY, ANONYMOUS_USER_ID);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([anonymousUserInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { getSession: () => of(session) } },
        { provide: AlertService, useValue: { showError: vi.fn(), showSuccess: vi.fn() } },
        { provide: TranslateService, useValue: { instant: (key: string) => key } },
      ],
    });

    service = TestBed.inject(ExportService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(STORAGE_KEY);
  });

  it('claims anonymous exports before fetching authenticated history', async () => {
    const historyPromise = firstValueFrom(service.getHistory());

    const claimRequest = httpTesting.expectOne('/api/export/csv/claim-anonymous');
    expect(claimRequest.request.method).toBe('POST');
    expect(claimRequest.request.headers.get('X-Anonymous-User-Id')).toBe(ANONYMOUS_USER_ID);
    claimRequest.flush({ claimedCount: 1 });

    httpTesting.expectOne('/api/export/csv/history').flush([]);
    await expect(historyPromise).resolves.toEqual([]);
  });

  it('only claims once after success', async () => {
    const firstHistory = firstValueFrom(service.getHistory());
    httpTesting.expectOne('/api/export/csv/claim-anonymous').flush({ claimedCount: 1 });
    httpTesting.expectOne('/api/export/csv/history').flush([]);
    await firstHistory;

    const secondHistory = firstValueFrom(service.getHistory());
    httpTesting.expectOne('/api/export/csv/history').flush([]);
    await secondHistory;
  });

  it('does not claim exports for anonymous history requests', async () => {
    session = null;

    const historyPromise = firstValueFrom(service.getHistory());

    httpTesting.expectNone('/api/export/csv/claim-anonymous');
    httpTesting.expectOne('/api/export/csv/history').flush([]);
    await expect(historyPromise).resolves.toEqual([]);
  });
});
