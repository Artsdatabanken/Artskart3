import { localeFromLanguage } from './locale';

describe('localeFromLanguage', () => {
  it('should map "en" to en-GB', () => {
    expect(localeFromLanguage('en')).toBe('en-GB');
  });

  it('should map "no" to nb-NO', () => {
    expect(localeFromLanguage('no')).toBe('nb-NO');
  });

  it('should fall back to nb-NO for unknown or empty languages', () => {
    expect(localeFromLanguage('de')).toBe('nb-NO');
    expect(localeFromLanguage('')).toBe('nb-NO');
  });
});
