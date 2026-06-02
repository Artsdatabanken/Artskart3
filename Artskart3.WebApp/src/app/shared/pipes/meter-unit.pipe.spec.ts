import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MeterUnitPipe } from './meter-unit.pipe';

describe('MeterUnitPipe', () => {
  let pipe: MeterUnitPipe;
  let translateService: TranslateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [MeterUnitPipe],
    });
    pipe = TestBed.inject(MeterUnitPipe);
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

  it('should format number with meter unit in Norwegian', () => {
    translateService.use('no');
    const result = pipe.transform(250);
    expect(result).toContain('250');
    expect(result).toContain('m');
  });

  it('should format number with meter unit in English', () => {
    translateService.use('en');
    const result = pipe.transform(250);
    expect(result).toContain('250');
    expect(result).toContain('m');
  });

  it('should format zero', () => {
    translateService.use('en');
    const result = pipe.transform(0);
    expect(result).toContain('0');
    expect(result).toContain('m');
  });
});
