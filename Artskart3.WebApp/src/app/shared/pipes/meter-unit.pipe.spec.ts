import { MeterUnitPipe } from './meter-unit.pipe';

describe('MeterUnitPipe', () => {
  const pipe = new MeterUnitPipe();

  it('should create', () => {
    expect(pipe).toBeTruthy();
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null, 'no')).toBe('');
  });

  it('should return empty string for undefined', () => {
    expect(pipe.transform(undefined, 'no')).toBe('');
  });

  it('should format number with meter unit in Norwegian', () => {
    const result = pipe.transform(250, 'no');
    expect(result).toContain('250');
    expect(result).toContain('m');
  });

  it('should format number with meter unit in English', () => {
    const result = pipe.transform(250, 'en');
    expect(result).toContain('250');
    expect(result).toContain('m');
  });

  it('should spell out the unit when asked for the long form', () => {
    expect(pipe.transform(100, 'no', 'long')).toBe('100 meter');
    expect(pipe.transform(100, 'en', 'long')).toBe('100 metres');
  });

  it('should format zero', () => {
    const result = pipe.transform(0, 'en');
    expect(result).toContain('0');
    expect(result).toContain('m');
  });
});
