import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage } from './api-error';

const FALLBACK = 'Kunne ikke starte eksport. Prøv igjen senere.';

describe('apiErrorMessage', () => {
  function httpError(status: number, body: unknown): HttpErrorResponse {
    return new HttpErrorResponse({ status, error: body });
  }

  /**
   * De tre grunnene eksport-endepunktene faktisk svarer med. Alle tre kollapset
   * tidligere til den samme setningen, og det var derfor den opprinnelige
   * feilrapporten var «det er ikke tydelig hvorfor».
   */
  it.each([
    [409, 'Maks 3 samtidige eksportjobber per bruker.'],
    [409, 'Antall rader overstiger grensen (500000).'],
    [401, "Bruker mangler 'sub'-claim."],
  ])('viser serverens grunn ved %i', (status, message) => {
    expect(apiErrorMessage(httpError(status, { error: message }), FALLBACK)).toBe(message);
  });

  it('plukker første melding fra modellvalidering', () => {
    const body = {
      title: 'One or more validation errors occurred.',
      errors: { Name: ['Navnet kan ikke være lengre enn 200 tegn.'] },
    };

    expect(apiErrorMessage(httpError(400, body), FALLBACK)).toBe('Navnet kan ikke være lengre enn 200 tegn.');
  });

  it('faller tilbake til detail og title når det ikke finnes noe bedre', () => {
    expect(apiErrorMessage(httpError(500, { detail: 'Noe gikk galt.' }), FALLBACK)).toBe('Noe gikk galt.');
    expect(apiErrorMessage(httpError(500, { title: 'Serverfeil' }), FALLBACK)).toBe('Serverfeil');
  });

  it.each([
    ['tom kropp', null],
    ['tekstkropp', 'Internal Server Error'],
    ['tom feilmelding', { error: '   ' }],
  ])('bruker standardteksten ved %s', (_label, body) => {
    expect(apiErrorMessage(httpError(500, body), FALLBACK)).toBe(FALLBACK);
  });

  it('bruker standardteksten for feil som ikke er HTTP-feil', () => {
    expect(apiErrorMessage(new Error('nettverksfeil'), FALLBACK)).toBe(FALLBACK);
  });
});
