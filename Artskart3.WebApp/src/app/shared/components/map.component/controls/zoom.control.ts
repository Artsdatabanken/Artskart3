import Zoom from 'ol/control/Zoom';
import { createSvgIcon } from './control-utils';

const ZOOM_IN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M11 13H5V11H11V5H13V11H19V13H13V19H11V13Z" fill="currentColor"/>
</svg>`;

const ZOOM_OUT_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M5 11H19V13H5V11Z" fill="currentColor"/>
</svg>`;

export interface ZoomControlLabels {
  zoomInTipLabel: string;
  zoomOutTipLabel: string;
}

export class ArtskartZoomControl extends Zoom {
  constructor(labels: ZoomControlLabels) {
    super({
      className: 'artskart-zoom',
      zoomInClassName: 'artskart-zoom-in',
      zoomOutClassName: 'artskart-zoom-out',
      zoomInLabel: createSvgIcon(ZOOM_IN_SVG),
      zoomOutLabel: createSvgIcon(ZOOM_OUT_SVG),
      zoomInTipLabel: labels.zoomInTipLabel,
      zoomOutTipLabel: labels.zoomOutTipLabel,
    });
    // OL sets title from tipLabel — clear it and use aria-label instead
    this.updateLabels(labels);
  }

  updateLabels(labels: ZoomControlLabels): void {
    const buttons = this.element.querySelectorAll('button');
    if (buttons[0]) {
      buttons[0].title = '';
      buttons[0].setAttribute('aria-label', labels.zoomInTipLabel);
    }
    if (buttons[1]) {
      buttons[1].title = '';
      buttons[1].setAttribute('aria-label', labels.zoomOutTipLabel);
    }
  }
}
