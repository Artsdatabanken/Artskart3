import { LookupNamePipe } from './lookup-name.pipe';

describe('LookupNamePipe', () => {
  let pipe: LookupNamePipe;
  let map: Map<number, string>;

  beforeEach(() => {
    pipe = new LookupNamePipe();
    map = new Map([
      [1, 'Kritisk truet'],
      [2, 'Sterkt truet'],
    ]);
  });

  it('should return the name for a known id', () => {
    expect(pipe.transform(1, map)).toBe('Kritisk truet');
    expect(pipe.transform(2, map)).toBe('Sterkt truet');
  });

  it('should return empty string for unknown id', () => {
    expect(pipe.transform(99, map)).toBe('');
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null, map)).toBe('');
  });

  it('should return empty string for undefined', () => {
    expect(pipe.transform(undefined, map)).toBe('');
  });
});
