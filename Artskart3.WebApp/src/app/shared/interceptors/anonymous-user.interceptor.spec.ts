import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ANONYMOUS_USER_HEADER, anonymousUserInterceptor } from './anonymous-user.interceptor';

const STORAGE_KEY = 'artskart.anonymousUserId';
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

describe('anonymousUserInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([anonymousUserInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(STORAGE_KEY);
  });

  it('legger på en generert GUID som lagres i localStorage', () => {
    http.get('/api/export/csv/history').subscribe();
    const req = httpTesting.expectOne('/api/export/csv/history');

    const header = req.request.headers.get(ANONYMOUS_USER_HEADER);
    expect(header).toMatch(UUID_PATTERN);
    expect(localStorage.getItem(STORAGE_KEY)).toBe(header);

    req.flush([]);
  });

  it('gjenbruker den samme ID-en på tvers av kall', () => {
    const existingId = crypto.randomUUID();
    localStorage.setItem(STORAGE_KEY, existingId);

    http.get('/api/export/csv/history').subscribe();
    http.post('/api/export/csv/start', {}).subscribe();

    const requests = httpTesting.match((r) => r.url.includes('/api/export/'));
    expect(requests.length).toBe(2);
    for (const req of requests) {
      expect(req.request.headers.get(ANONYMOUS_USER_HEADER)).toBe(existingId);
      req.flush([]);
    }
  });

  it('rører ikke kall til andre endepunkter', () => {
    http.get('/api/observations').subscribe();
    const req = httpTesting.expectOne('/api/observations');

    expect(req.request.headers.has(ANONYMOUS_USER_HEADER)).toBe(false);
    req.flush([]);
  });
});