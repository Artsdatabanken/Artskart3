import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LocaleDatePipe } from './locale-date.pipe';

describe('LocaleDatePipe', () => {
  let pipe: LocaleDatePipe;
  let translateService: TranslateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [LocaleDatePipe],
    });
    pipe = TestBed.inject(LocaleDatePipe);
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

  it('should format date in Norwegian when lang is "no"', () => {
    translateService.use('no');
    const result = pipe.transform('2026-05-23T00:00:00');
    expect(result).toBe('23. mai 2026');
  });

  it('should format date in English when lang is "en"', () => {
    translateService.use('en');
    const result = pipe.transform('2026-05-23T00:00:00');
    expect(result).toBe('23 May 2026');
  });

  it('should handle date-only strings', () => {
    translateService.use('en');
    const result = pipe.transform('1936-08-11');
    expect(result).toBe('11 August 1936');
  });
});
