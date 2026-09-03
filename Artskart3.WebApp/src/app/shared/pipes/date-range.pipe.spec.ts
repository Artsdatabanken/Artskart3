import { describe, expect, it } from 'vitest';
import { DateRangePipe } from './date-range.pipe';

describe('DateRangePipe', () => {
  const pipe = new DateRangePipe();

  it('returns an empty string when both dates are absent', () => {
    expect(pipe.transform()).toBe('');
  });

  it('returns an empty string for invalid dates', () => {
    expect(pipe.transform('not-a-date')).toBe('');
  });

  it('formats one date', () => {
    expect(pipe.transform('2026-07-01', undefined, 'no')).toBe('01. juli 2026');
  });

  it('formats a date range', () => {
    expect(pipe.transform('2026-07-01', '2026-07-31', 'no')).toBe('01. juli 2026 - 31. juli 2026');
  });

  it('formats dates in English', () => {
    expect(pipe.transform('2026-07-01', '2026-07-31', 'en')).toBe('01 July 2026 - 31 July 2026');
  });
});