import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ANONYMOUS_USER_HEADER, anonymousUserInterceptor } from './anonymous-user.interceptor';
import { AuthService } from '../services/auth/auth.service';

const STORAGE_KEY = 'artskart.anonymousUserId';
const SESSION_URL = '/api/export/csv/anonymous-session';
const SERVER_TOKEN = 'signert-token-fra-server';

describe('anonymousUserInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let anonymous: boolean;

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    anonymous = true;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([anonymousUserInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAnonymous: () => anonymous } },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(STORAGE_KEY);
  });

  it('henter token fra serveren ved første eksportkall og lagrer det', () => {
    http.get('/api/export/csv/history').subscribe();

    httpTesting.expectOne(SESSION_URL).flush({ token: SERVER_TOKEN });

    const req = httpTesting.expectOne('/api/export/csv/history');
    expect(req.request.headers.get(ANONYMOUS_USER_HEADER)).toBe(SERVER_TOKEN);
    expect(localStorage.getItem(STORAGE_KEY)).toBe(SERVER_TOKEN);
    req.flush([]);
  });

  it('gjenbruker lagra token uten nytt serverkall', () => {
    localStorage.setItem(STORAGE_KEY, SERVER_TOKEN);

    http.get('/api/export/csv/history').subscribe();
    http.post('/api/export/csv/start', {}).subscribe();

    httpTesting.expectNone(SESSION_URL);
    const requests = httpTesting.match((r) => r.url.includes('/api/export/'));
    expect(requests.length).toBe(2);
    for (const req of requests) {
      expect(req.request.headers.get(ANONYMOUS_USER_HEADER)).toBe(SERVER_TOKEN);
      req.flush([]);
    }
  });

  it('sender ikke headeren for innloggede brukere', () => {
    anonymous = false;
    localStorage.setItem(STORAGE_KEY, SERVER_TOKEN);

    http.get('/api/export/csv/history').subscribe();

    const req = httpTesting.expectOne('/api/export/csv/history');
    expect(req.request.headers.has(ANONYMOUS_USER_HEADER)).toBe(false);
    req.flush([]);
  });

  it('sender lagra token på claim-kallet også når brukeren er innlogget', () => {
    anonymous = false;
    localStorage.setItem(STORAGE_KEY, SERVER_TOKEN);

    http.post('/api/export/csv/claim-anonymous', {}).subscribe();

    const req = httpTesting.expectOne('/api/export/csv/claim-anonymous');
    expect(req.request.headers.get(ANONYMOUS_USER_HEADER)).toBe(SERVER_TOKEN);
    req.flush({ claimedCount: 1 });
  });

  it('dropper claim-headeren når det ikke finnes noe token', () => {
    anonymous = false;

    http.post('/api/export/csv/claim-anonymous', {}).subscribe();

    const req = httpTesting.expectOne('/api/export/csv/claim-anonymous');
    expect(req.request.headers.has(ANONYMOUS_USER_HEADER)).toBe(false);
    req.flush({ claimedCount: 0 });
  });

  it('rører ikke kall til andre endepunkter', () => {
    http.get('/api/observations').subscribe();
    const req = httpTesting.expectOne('/api/observations');

    expect(req.request.headers.has(ANONYMOUS_USER_HEADER)).toBe(false);
    req.flush([]);
  });
});
