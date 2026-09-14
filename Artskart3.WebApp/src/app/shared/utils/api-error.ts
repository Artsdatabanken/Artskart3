import { HttpErrorResponse } from '@angular/common/http';

/**
 * Henter ut den faktiske feilmeldingen fra et API-svar.
 *
 * Grunnen til at dette finnes: eksport-endepunktene svarer med genuint
 * forskjellige og handlingsrettede grunner — «Maks 3 samtidige eksportjobber per
 * bruker», «Antall rader overstiger grensen (500000)», «Bruker mangler
 * 'sub'-claim» — men frontend kastet responsen og viste den samme setningen
 * uansett: «Kunne ikke starte eksport. Prøv igjen senere.»
 *
 * Det gjorde tre helt ulike problemer umulige å skille fra hverandre, både for
 * brukeren og for den som skulle feilsøke. Den opprinnelige feilrapporten var
 * bokstavelig talt «det er ikke tydelig hvorfor».
 *
 * Formater som håndteres:
 *  - `{ error: "..." }` — det API-ets eksport-endepunkter bruker
 *  - ProblemDetails med `errors` — modellvalidering, f.eks. for langt navn
 *  - ProblemDetails med `title`/`detail` — resten av ASP.NET-feilene
 */
export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;

  const body = error.error;
  if (!body || typeof body !== 'object') return fallback;

  const record = body as Record<string, unknown>;

  if (typeof record['error'] === 'string' && record['error'].trim()) {
    return record['error'];
  }

  // Modellvalidering: { errors: { Name: ["Navnet kan ikke være ..."] } }
  const errors = record['errors'];
  if (errors && typeof errors === 'object') {
    const firstMessage = Object.values(errors as Record<string, unknown>)
      .flatMap((messages) => (Array.isArray(messages) ? messages : [messages]))
      .find((message): message is string => typeof message === 'string' && message.trim().length > 0);

    if (firstMessage) return firstMessage;
  }

  for (const key of ['detail', 'title'] as const) {
    const value = record[key];
    if (typeof value === 'string' && value.trim()) return value;
  }

  return fallback;
}
