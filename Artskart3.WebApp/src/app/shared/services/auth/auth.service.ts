import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, Signal, computed, inject, signal } from '@angular/core';
import { catchError, shareReplay, tap, Observable } from 'rxjs';
import { of } from 'rxjs';
import { clearAnonymousToken } from '../../utils/anonymous-user-token';

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
  private readonly destroyRef = inject(DestroyRef);
  private session$: Observable<Session> | null = null;
  private refreshInFlight = false;
  private authChannel: BroadcastChannel | null = null;

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
  private readonly sessionResolvedSignal = signal(false);

  public session: Signal<Session> = this.sessionSignal.asReadonly();
  public sessionResolved: Signal<boolean> = this.sessionResolvedSignal.asReadonly();

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

  private readonly onSessionMessage = (event: MessageEvent<unknown>): void => {
    if (event.data === 'logout') {
      clearAnonymousToken();
      this.refreshSession();
    } else if (event.data === 'session-changed') {
      this.refreshSession();
    }
  };

  private readonly onWindowFocus = (): void => {
    if (this.sessionResolvedSignal()) this.refreshSession();
  };

  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') this.onWindowFocus();
  };

  constructor() {
    this.getSession().subscribe();

    if (typeof window !== 'undefined') {
      if (typeof BroadcastChannel !== 'undefined') {
        this.authChannel = new BroadcastChannel('artskart-auth');
        this.authChannel.addEventListener('message', this.onSessionMessage);
      }

      window.addEventListener('focus', this.onWindowFocus);
      if (typeof document !== 'undefined') {
        document.addEventListener('visibilitychange', this.onVisibilityChange);
      }

      this.destroyRef.onDestroy(() => {
        this.authChannel?.removeEventListener('message', this.onSessionMessage);
        this.authChannel?.close();
        window.removeEventListener('focus', this.onWindowFocus);
        if (typeof document !== 'undefined') {
          document.removeEventListener('visibilitychange', this.onVisibilityChange);
        }
      });
    }
  }

  public getSession(ignoreCache = false): Observable<Session> {
    if (!this.session$ || ignoreCache) {
      this.session$ = this.http.get<Session>('bff/user').pipe(
        catchError(() => of(ANONYMOUS)),
        tap((session) => {
          const wasResolved = this.sessionResolvedSignal();
          const previousSession = this.sessionSignal();
          const changed = !this.sessionsAreEqual(previousSession, session);

          this.sessionSignal.set(session);
          this.sessionResolvedSignal.set(true);

          if (wasResolved && previousSession !== null && session === null) {
            clearAnonymousToken();
            this.notifyOtherTabs('logout');
          } else if ((!wasResolved && session !== null) || (wasResolved && changed)) {
            this.notifyOtherTabs('session-changed');
          }
        }),
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
    this.notifyOtherTabs('session-changed');
    const returnUrl = window.location.pathname + window.location.search;
    window.location.href = `/bff/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  public logout(): void {
    const url = this.logoutUrl();
    clearAnonymousToken();
    this.notifyOtherTabs('logout');
    if (url) {
      window.location.href = url;
    }
  }

  private sessionsAreEqual(left: Session, right: Session): boolean {
    if (left === null || right === null) return left === right;
    if (left.length !== right.length) return false;

    return left.every((claim, index) => claim.type === right[index].type && claim.value === right[index].value);
  }

  private notifyOtherTabs(message: 'session-changed' | 'logout'): void {
    this.authChannel?.postMessage(message);
  }
}
