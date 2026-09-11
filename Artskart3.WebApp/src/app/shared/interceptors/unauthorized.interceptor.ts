import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
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
  // Selve sesjonsoppslaget slipper forbi urørt. En 401 derfra BETYR bare at
  // brukeren ikke er innlogget — å reagere på den ville satt i gang et nytt
  // oppslag mot det samme endepunktet.
  //
  // Denne testen må stå FØR alt annet i interceptoren, ikke inne i catchError.
  // AuthService henter bff/user fra sin egen konstruktør, og det kallet går
  // gjennom denne interceptoren mens tjenesten fortsatt konstrueres. Ba vi DI om
  // AuthService her, ville vi bedt om en tjeneste som er midt i konstruksjon —
  // NG0200, sirkulær avhengighet, ved aller første HTTP-kall i appen.
  if (req.url.includes('bff/')) {
    return next(req);
  }

  // Injector, ikke AuthService direkte. Oppslaget utsettes til feilen faktisk
  // inntreffer, altså lenge etter at konstruktøren er ferdig. Det holder
  // interceptoren trygg også hvis noen senere legger til flere kall som skjer
  // under oppstart.
  const injector = inject(Injector);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        // Tvinger et nytt oppslag mot bff/user. Signalet oppdateres, og
        // grensesnittet slutter å tilby handlinger brukeren ikke har tilgang til.
        injector.get(AuthService).refreshSession();
      }

      return throwError(() => error);
    }),
  );
};
