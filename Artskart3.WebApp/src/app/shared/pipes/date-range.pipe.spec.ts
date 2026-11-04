import { describe, expect, it } from 'vitest';
import { DateRangePipe } from './date-range.pipe';

describe('DateRangePipe', () => {
  const pipe = new DateRangePipe();

  it('returns an empty string when both dates are absent', () => {
    expect(pipe.transform(undefined, undefined, 'no')).toBe('');
  });

  it('returns an empty string for invalid dates', () => {
    expect(pipe.transform('not-a-date', undefined, 'no')).toBe('');
    expect(pipe.transform('2026-02-31', undefined, 'no')).toBe('');
  });

  it('formats one date', () => {
    expect(pipe.transform('2026-07-01', undefined, 'no')).toBe('01. juli 2026');
  });

  it('formats a date range', () => {
    expect(pipe.transform('2026-07-01', '2026-07-31', 'no')).toBe('01. juli 2026 - 31. juli 2026');
  });

  it('includes the time when the value has one', () => {
    expect(pipe.transform('2026-07-01T08:30:00', '2026-07-01T10:00:00', 'no')).toBe(
      '01. juli 2026 kl. 08:30 - 01. juli 2026 kl. 10:00'
    );
  });

  it('accepts fractional seconds and rejects invalid times', () => {
    expect(pipe.transform('2026-07-01T08:30:00.1234567', undefined, 'no')).toBe('01. juli 2026 kl. 08:30');
    expect(pipe.transform('2026-07-01T25:00:00', undefined, 'no')).toBe('');
  });

  it('formats dates in English', () => {
    expect(pipe.transform('2026-07-01', '2026-07-31', 'en')).toBe('01 July 2026 - 31 July 2026');
  });
});