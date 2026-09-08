import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth/auth.service';

/**
 * Oppdager at BFF-sesjonen har gått ut, og oppdaterer innloggingstilstanden.
 *
 * API-controllerne er mappet med .AsBffApiEndpoint(), så en utløpt sesjon gir
 * 401 — ikke en redirect til innlogging. AuthService henter `bff/user` én gang
 * og cacher med shareReplay(1), så uten dette forble `isAuthenticated()` true
 * for hele levetiden til fanen.
 *
 * Resultatet var at eksportknappen ble stående synlig og aktiv, hvert klikk
 * feilet med en generell toast, og bare en full sideoppdatering avslørte at
 * brukeren var logget ut. Det er den mest sannsynlige forklaringen på «eksporter
 * blir ikke opprettet» når det ikke finnes en eneste rad i databasen: kallet kom
 * aldri forbi autorisasjonen.
 *
 * `bff/`-kallene er unntatt — de er selve sesjonsoppslaget, og å reagere på en
 * 401 derfra ville satt i gang en ny runde med det samme kallet.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const isSessionRequest = req.url.includes('bff/');

      if (error instanceof HttpErrorResponse && error.status === 401 && !isSessionRequest) {
        // Tvinger et nytt oppslag mot bff/user. Signalet oppdateres, og
        // grensesnittet slutter å tilby handlinger brukeren ikke har tilgang til.
        authService.refreshSession();
      }

      return throwError(() => error);
    }),
  );
};
