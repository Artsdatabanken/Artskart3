import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { AuthService } from '../services/auth/auth.service';
import { peekAnonymousToken, storeAnonymousToken } from '../utils/anonymous-user-token';

const SESSION_URL = '/api/export/csv/anonymous-session';
export const ANONYMOUS_USER_HEADER = 'X-Anonymous-User-Id';

let tokenRequest$: Observable<string> | null = null;

/**
 * Anonyme brukere eier eksportjobbene sine via et signert token utstedt av
 * serveren. Signaturen gjør at backend kan skille anonyme ID-er fra innloggede
 * brukeres sub, så en vilkårlig GUID i headeren ikke lenger godtas som eier.
 * Tokenet ligger i localStorage, så historikken overlever nye økter så lenge
 * brukeren er på samme nettleser og maskin.
 */

function getAnonymousToken(http: HttpClient): Observable<string> {
  const existing = peekAnonymousToken();
  if (existing) return of(existing);

  // Del pågående forespørsel, så parallelle eksportkall ikke henter hvert sitt token.
  tokenRequest$ ??= http.post<{ token: string }>(SESSION_URL, {}).pipe(
    map((response) => response.token),
    tap(storeAnonymousToken),
    finalize(() => (tokenRequest$ = null)),
    shareReplay(1),
  );
  return tokenRequest$;
}

export const anonymousUserInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/export/') || req.url.includes(SESSION_URL)) return next(req);

  // Lazy inject per kall bryter DI-syklusen HttpClient -> interceptor -> AuthService.
  const authService = inject(AuthService);

  if (!authService.isAnonymous()) {
    // Innlogget: bare claim-kallet trenger headeren, og bare hvis det finnes et
    // token å overføre. Å sende den ellers ville gjort at en utløpt sesjon laget
    // anonyme jobber i stedet for å gi 401 og utløse refreshSession.
    if (req.url.includes('/claim-anonymous')) {
      const token = peekAnonymousToken();
      if (token) return next(req.clone({ setHeaders: { [ANONYMOUS_USER_HEADER]: token } }));
    }
    return next(req);
  }

  return getAnonymousToken(inject(HttpClient)).pipe(
    switchMap((token) => next(req.clone({ setHeaders: { [ANONYMOUS_USER_HEADER]: token } }))),
  );
};
