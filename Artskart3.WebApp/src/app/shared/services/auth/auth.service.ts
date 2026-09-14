import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { catchError, shareReplay, tap, Observable } from 'rxjs';
import { of } from 'rxjs';

export interface Claim {
  type: string;
  value: string;
}

export type Session = Claim[] | null;

const ANONYMOUS: Session = null;

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private session$: Observable<Session> | null = null;
  private refreshInFlight = false;

  /**
   * Sesjonen er et skrivbart signal, ikke `toSignal` over et engangskall.
   *
   * Tidligere ble `bff/user` hentet én gang og cachet med shareReplay(1), og
   * signalet ble aldri vurdert på nytt så lenge fanen levde. Gikk sesjonen ut,
   * fortsatte grensesnittet å tro at brukeren var innlogget: eksportknappen sto
   * synlig og aktiv, hvert klikk feilet med en generell toast, og bare en full
   * sideoppdatering avslørte hva som var galt.
   *
   * Nå kan unauthorizedInterceptor be om en ny vurdering når API-et svarer 401.
   */
  private readonly sessionSignal = signal<Session>(ANONYMOUS);

  public session: Signal<Session> = this.sessionSignal.asReadonly();

  public isAuthenticated = computed(() => this.session() !== null);
  public isAnonymous = computed(() => this.session() === null);

  public username = computed(() => {
    const session = this.session();
    return session ? session.find((c) => c.type === 'name')?.value || null : null;
  });

  public logoutUrl = computed(() => {
    const session = this.session();
    return session ? session.find((c) => c.type === 'bff:logout_url')?.value || null : null;
  });

  constructor() {
    this.getSession().subscribe();
  }

  public getSession(ignoreCache = false): Observable<Session> {
    if (!this.session$ || ignoreCache) {
      this.session$ = this.http.get<Session>('bff/user').pipe(
        catchError(() => of(ANONYMOUS)),
        tap((session) => this.sessionSignal.set(session)),
        shareReplay(1)
      );
    }
    return this.session$;
  }

  /**
   * Henter sesjonen på nytt og oppdaterer signalet. Kalles når API-et svarer 401.
   *
   * Flagget hindrer at en side som fyrer av flere kall samtidig utløser like
   * mange parallelle sesjonsoppslag — alle 401-ene kommer fra den samme utløpte
   * sesjonen, og ett oppslag er nok til å fastslå det.
   */
  public refreshSession(): void {
    if (this.refreshInFlight) return;

    this.refreshInFlight = true;
    this.getSession(true).subscribe({
      next: () => (this.refreshInFlight = false),
      error: () => (this.refreshInFlight = false),
    });
  }

  public login(): void {
    const returnUrl = window.location.pathname + window.location.search;
    window.location.href = `/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  public logout(): void {
    const url = this.logoutUrl();
    if (url) {
      window.location.href = url;
    }
  }
}
