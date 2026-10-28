import { HttpInterceptorFn } from '@angular/common/http';

const STORAGE_KEY = 'artskart.anonymousUserId';
export const ANONYMOUS_USER_HEADER = 'X-Anonymous-User-Id';

let inMemoryId: string | null = null;

/**
 * Anonyme brukere eier eksportjobbene sine via en klientgenerert GUID som
 * backend lagrer som UserId. GUID-en ligger i localStorage, så historikken
 * overlever nye økter så lenge brukeren er på samme nettleser og maskin.
 */
function getAnonymousUserId(): string {
  try {
    const existing = localStorage.getItem(STORAGE_KEY);
    if (existing) return existing;

    const id = crypto.randomUUID();
    localStorage.setItem(STORAGE_KEY, id);
    return id;
  } catch {
    // localStorage utilgjengelig (f.eks. privat modus) — ID-en lever bare så lenge fanen gjør.
    inMemoryId ??= crypto.randomUUID();
    return inMemoryId;
  }
}

export const anonymousUserInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/export/')) return next(req);

  return next(req.clone({ setHeaders: { [ANONYMOUS_USER_HEADER]: getAnonymousUserId() } }));
};
