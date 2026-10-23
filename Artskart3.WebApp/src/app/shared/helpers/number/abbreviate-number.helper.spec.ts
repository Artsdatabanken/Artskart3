import { AbbreviateNumberHelper } from './abbreviate-number.helper';

describe('AbbreviateNumberHelper', () => {
  it.each([
    [999, '999', '999'],
    [1000, '1 k', '1k'],
    [8456, '8,5 k', '8.5k'],
    [24333, '24,3 k', '24.3k'],
    [99949, '99,9 k', '99.9k'],
    [99950, '100 k', '100k'],
    [100000, '100 k', '100k'],
    [243333, '243 k', '243k'],
    [8000000, '8 M', '8M'],
    [12345678, '12,3 M', '12.3M'],
    [123456789, '123 M', '123M'],
  ])('formats %i as %s in Norwegian and %s in English', (num, norwegian, english) => {
    expect(AbbreviateNumberHelper.format(num, 'no')).toBe(norwegian);
    expect(AbbreviateNumberHelper.format(num, 'en')).toBe(english);
  });
});
