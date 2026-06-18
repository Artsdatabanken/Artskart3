import { Injectable } from '@angular/core';
import { ApiMessages, ZoomLevelMapping } from '@core/constants/api-messages';

@Injectable({
  providedIn: 'root'
})
export class ValidationService {
  validateZoomLevel(zoom: number): { valid: boolean; error?: string; normalized?: number } {
    if (!Number.isFinite(zoom)) {
      return { valid: false, error: ApiMessages.Errors.InvalidParameters };
    }

    const normalized = Math.round(zoom);

    if (normalized < ZoomLevelMapping.MIN_ZOOM || normalized > ZoomLevelMapping.MAX_ZOOM) {
      return {
        valid: false,
        error: `Zoom level must be between ${ZoomLevelMapping.MIN_ZOOM} and ${ZoomLevelMapping.MAX_ZOOM}`
      };
    }

    return { valid: true, normalized };
  }
}
