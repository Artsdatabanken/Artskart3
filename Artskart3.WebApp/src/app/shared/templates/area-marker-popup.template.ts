import { AreaMarkerFeature } from '@shared/models/area/area-marker.model';

export class AreaMarkerPopupTemplate {
  static createPopup(feature: AreaMarkerFeature): string {
    const { name, areaTypeName, observationCount } = feature.properties;
    const countDisplay = observationCount !== null
      ? `<span style="color: #ff7800; font-weight: 500;">📊 ${observationCount.toLocaleString()} observations</span>`
      : '';

    return `
      <div style="font-size: 13px; line-height: 1.6; padding: 8px;">
        <strong>${name}</strong><br/>
        <span style="color: #666; font-size: 12px;">${areaTypeName}</span><br/>
        ${countDisplay}
      </div>
    `.trim();
  }
}
