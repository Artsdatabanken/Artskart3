import { LocaleDatePipe } from './locale-date.pipe';

describe('LocaleDatePipe', () => {
  const pipe = new LocaleDatePipe();

  it('should create', () => {
    expect(pipe).toBeTruthy();
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('should return empty string for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('should return empty string for empty string', () => {
    expect(pipe.transform('')).toBe('');
  });

  it('should return empty string for invalid date', () => {
    expect(pipe.transform('not-a-date')).toBe('');
  });

  it('should format date in Norwegian when lang is "no"', () => {
    const result = pipe.transform('2026-05-23T00:00:00', 'no');
    expect(result).toBe('23. mai 2026');
  });

  it('should format date in English when lang is "en"', () => {
    const result = pipe.transform('2026-05-23T00:00:00', 'en');
    expect(result).toBe('23 May 2026');
  });

  it('should handle date-only strings', () => {
    const result = pipe.transform('1936-08-11', 'en');
    expect(result).toBe('11 August 1936');
  });
});
