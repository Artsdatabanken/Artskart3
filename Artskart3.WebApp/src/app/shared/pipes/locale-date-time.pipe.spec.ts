import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LocaleDateTimePipe } from './locale-date-time.pipe';

describe('LocaleDateTimePipe', () => {
  let pipe: LocaleDateTimePipe;
  let translateService: TranslateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [LocaleDateTimePipe],
    });
    pipe = TestBed.inject(LocaleDateTimePipe);
    translateService = TestBed.inject(TranslateService);
  });

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
    translateService.use('no');
    const result = pipe.transform('2026-05-23T14:30:00');
    expect(result).toContain('23.');
    expect(result).toContain('mai');
    expect(result).toContain('2026');
    expect(result).toContain('14:30');
  });

  it('should format date with time in English when lang is "en"', () => {
    translateService.use('en');
    const result = pipe.transform('2026-05-23T14:30:00');
    expect(result).toContain('23');
    expect(result).toContain('May');
    expect(result).toContain('2026');
    expect(result).toContain('14:30');
  });

  it('should handle midnight correctly', () => {
    translateService.use('no');
    const result = pipe.transform('2026-01-01T00:00:00');
    expect(result).toContain('00:00');
  });
});
