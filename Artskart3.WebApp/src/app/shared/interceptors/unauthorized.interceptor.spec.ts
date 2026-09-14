import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { unauthorizedInterceptor } from './unauthorized.interceptor';
import { AuthService } from '../services/auth/auth.service';

/**
 * En utløpt BFF-sesjon gir 401, ikke en redirect. Uten dette forble
 * innloggingstilstanden i frontend «innlogget» for hele levetiden til fanen:
 * eksportknappen sto aktiv, hvert klikk feilet, og bare en full sideoppdatering
 * avslørte hva som var galt.
 */
describe('unauthorizedInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let authService: AuthService;

  const SESSION = [{ type: 'name', value: 'Kari' }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);

    // AuthService henter sesjonen når den opprettes.
    httpTesting.expectOne('bff/user').flush(SESSION);
  });

  afterEach(() => httpTesting.verify());

  it('henter sesjonen på nytt når API-et svarer 401', () => {
    expect(authService.isAuthenticated()).toBe(true);

    http.get('/api/export/csv/history').subscribe({ error: () => undefined });
    httpTesting.expectOne('/api/export/csv/history').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Sesjonen sjekkes på nytt, og en utlogget bruker gir anonym tilstand.
    httpTesting.expectOne('bff/user').flush(null);
    expect(authService.isAnonymous()).toBe(true);
  });

  it('sender feilen videre slik at kallstedet fortsatt kan vise en melding', () => {
    let status: number | undefined;

    http.get('/api/export/csv/start').subscribe({ error: (e) => (status = e.status) });
    httpTesting.expectOne('/api/export/csv/start').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpTesting.expectOne('bff/user').flush(null);

    expect(status).toBe(401);
  });

  /**
   * Uten dette unntaket ville en 401 fra selve sesjonsoppslaget utløst et nytt
   * sesjonsoppslag.
   */
  it('reagerer ikke på 401 fra sesjonsoppslaget selv', () => {
    http.get('bff/user').subscribe({ error: () => undefined });
    httpTesting.expectOne('bff/user').flush(null, { status: 401, statusText: 'Unauthorized' });

    httpTesting.verify(); // ingen ekstra bff/user-kall
  });

  it('rører ikke svar som går bra', () => {
    http.get('/api/export/csv/history').subscribe();
    httpTesting.expectOne('/api/export/csv/history').flush([]);

    expect(authService.isAuthenticated()).toBe(true);
  });
});
