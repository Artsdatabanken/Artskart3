import { LocaleDateTimePipe } from './locale-date-time.pipe';

describe('LocaleDateTimePipe', () => {
  const pipe = new LocaleDateTimePipe();

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

  it('should format date with time in Norwegian when lang is "no"', () => {
    const result = pipe.transform('2026-05-23T14:30:00', 'no');
    expect(result).toContain('23.');
    expect(result).toContain('mai');
    expect(result).toContain('2026');
    expect(result).toContain('14:30');
  });

  it('should format date with time in English when lang is "en"', () => {
    const result = pipe.transform('2026-05-23T14:30:00', 'en');
    expect(result).toContain('23');
    expect(result).toContain('May');
    expect(result).toContain('2026');
    expect(result).toContain('14:30');
  });

  it('should handle midnight correctly', () => {
    const result = pipe.transform('2026-01-01T00:00:00', 'no');
    expect(result).toContain('00:00');
  });
});
