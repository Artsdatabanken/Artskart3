import { TestBed } from '@angular/core/testing';
import { of, firstValueFrom } from 'rxjs';
import { AreaNamePipe } from './area-name.pipe';
import { AreaService } from '../services/area/area.service';
import { AreaTypeDto } from '../types/api.types';

describe('AreaNamePipe', () => {
  let pipe: AreaNamePipe;

  const mockAreas: AreaTypeDto[] = [
    { id: 1, name: 'County', areas: [{ id: 1, fid: '03', name: 'Oslo', isCurrent: true }] },
    {
      id: 2,
      name: 'Municipality',
      areas: [{ id: 10, fid: '0301', name: 'Oslo kommune', isCurrent: true }],
    },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AreaNamePipe,
        { provide: AreaService, useValue: { getAreas: () => of(mockAreas) } },
      ],
    });
    pipe = TestBed.inject(AreaNamePipe);
  });

  it('should resolve fid to area name', async () => {
    const result = await firstValueFrom(pipe.transform('0301'));
    expect(result).toBe('Oslo kommune');
  });

  it('should resolve county fid to county name', async () => {
    const result = await firstValueFrom(pipe.transform('03'));
    expect(result).toBe('Oslo');
  });

  it('should return fid if not found', async () => {
    const result = await firstValueFrom(pipe.transform('9999'));
    expect(result).toBe('9999');
  });

  it('should return empty string for null', async () => {
    const result = await firstValueFrom(pipe.transform(null));
    expect(result).toBe('');
  });
});
