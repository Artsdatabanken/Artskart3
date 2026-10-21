import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { CadastralUnitDto } from '../../types/api.types';

const CADASTRAL_UNIT_ID = /^(\d{4})-(\d{1,5})\/(\d{1,5})(?:\/(\d{1,5}))?(?:\/(\d{1,5}))?$/;

/**
 * Canonical form of a matrikkelnummer like 1101-1/2, or null when the format is wrong.
 * Matches CadastralUnitId.Normalize in the API.
 */
export function normalizeCadastralUnitId(input: string): string | null {
  const match = CADASTRAL_UNIT_ID.exec(input.replace(/\s/g, ''));
  if (!match) return null;

  const [, municipality, ...parts] = match;
  const numbers = parts.filter((n) => n !== undefined).map(Number);
  // Festenummer and seksjonsnummer 0 mean the unit has none, so 1101-1/2/0 is 1101-1/2.
  while (numbers.length > 2 && numbers.at(-1) === 0) numbers.pop();
  return `${municipality}-${numbers.join('/')}`;
}

/**
 * Matrikkelenheter (gårds- og bruksnummer) from Kartverket, through our API. Kartverket
 * only finds complete numbers, so there is no prefix search to build suggestions on.
 */
@Service()
export class CadastralUnitService {
  private readonly http = inject(HttpClient);
  private readonly requests = new Map<string, Observable<CadastralUnitDto>>();

  /** Errors with status 404 when Kartverket doesn't know the number. */
  get(id: string): Observable<CadastralUnitDto> {
    let request = this.requests.get(id);
    if (!request) {
      request = this.http.get<CadastralUnitDto>('/api/Lookup/CadastralUnit', { params: { id } }).pipe(shareReplay(1));
      this.requests.set(id, request);
    }
    return request;
  }
}
