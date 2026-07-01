import { TestBed } from '@angular/core/testing';
import { ValidationService } from './validation.service';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';

describe('ValidationService', () => {
  let service: ValidationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ValidationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('validateZoomLevel - API zoom level consistency', () => {
    it('should produce the same API zoom level before and after normalization', () => {
      // Fractional zooms near boundaries that previously caused mismatches
      const edgeCases = [8.5, 8.7, 8.9, 10.5, 10.6, 10.9];

      for (const rawZoom of edgeCases) {
        const apiLevelFromRaw = ZoomConfig.getApiZoomLevel(rawZoom);
        const { normalized } = service.validateZoomLevel(rawZoom);
        const apiLevelFromNormalized = ZoomConfig.getApiZoomLevel(normalized!);

        expect(apiLevelFromNormalized, `Zoom ${rawZoom}: raw gives API level ${apiLevelFromRaw}, but normalized (${normalized}) gives ${apiLevelFromNormalized}`).toBe(apiLevelFromRaw);
      }
    });

    it('should floor fractional zoom levels, not round them', () => {
      expect(service.validateZoomLevel(10.6).normalized).toBe(10);
      expect(service.validateZoomLevel(10.9).normalized).toBe(10);
      expect(service.validateZoomLevel(8.5).normalized).toBe(8);
      expect(service.validateZoomLevel(11.0).normalized).toBe(11);
    });

    it('should keep integer zoom levels unchanged', () => {
      expect(service.validateZoomLevel(9).normalized).toBe(9);
      expect(service.validateZoomLevel(11).normalized).toBe(11);
      expect(service.validateZoomLevel(5).normalized).toBe(5);
    });
  });
});
