import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AreaNamePipe } from './area-name.pipe';
import { AreaService } from '../services/area/area.service';

describe('AreaNamePipe', () => {
  let pipe: AreaNamePipe;

  const mockMunicipalities = signal([
    { id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true },
  ]);

  const mockCounties = signal([
    { id: 1, fid: '03', name: 'Oslo', isCurrent: true },
  ]);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AreaNamePipe,
        { provide: AreaService, useValue: { municipalities: mockMunicipalities, counties: mockCounties } },
      ],
    });
    pipe = TestBed.inject(AreaNamePipe);
  });

  it('should resolve fid to municipality name', () => {
    expect(pipe.transform('0301')).toBe('Oslo kommune');
  });

  it('should resolve county fid to county name', () => {
    expect(pipe.transform('03')).toBe('Oslo');
  });

  it('should return fid if not found', () => {
    expect(pipe.transform('9999')).toBe('9999');
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
