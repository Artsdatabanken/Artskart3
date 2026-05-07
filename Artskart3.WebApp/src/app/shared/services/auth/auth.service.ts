import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, computed, inject } from '@angular/core';
import { catchError, shareReplay, Observable, defer } from 'rxjs';
import { of } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

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

  public session: Signal<Session> = toSignal(defer(() => this.getSession()), {
    initialValue: ANONYMOUS,
  });

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

  public getSession(ignoreCache: boolean = false): Observable<Session> {
    if (!this.session$ || ignoreCache) {
      this.session$ = this.http.get<Session>('bff/user').pipe(
        catchError(() => of(ANONYMOUS)),
        shareReplay(1)
      );
    }
    return this.session$;
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
