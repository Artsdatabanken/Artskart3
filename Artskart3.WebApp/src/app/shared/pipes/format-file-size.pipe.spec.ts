import { FormatFileSizePipe } from './format-file-size.pipe';

describe('FormatFileSizePipe', () => {
  const pipe = new FormatFileSizePipe();

  it('should show "< 1 MB" for sizes under 1 MB', () => {
    expect(pipe.transform(597504, 'no')).toBe('< 1 MB');
  });

  it('should format bytes in MB', () => {
    expect(pipe.transform(10485760, 'no')).toBe('10 MB');
  });

  it('should format bytes in GB', () => {
    expect(pipe.transform(1073741824, 'no')).toBe('1 GB');
  });

  it('should show "< 1 MB" for small values', () => {
    expect(pipe.transform(512, 'no')).toBe('< 1 MB');
  });

  it('should return "-" for null', () => {
    expect(pipe.transform(null)).toBe('-');
  });

  it('should return "-" for undefined', () => {
    expect(pipe.transform(undefined)).toBe('-');
  });

  it('should return "-" for zero', () => {
    expect(pipe.transform(0)).toBe('-');
  });
});
