import Zoom from 'ol/control/Zoom';
import { createSvgIcon } from './control-utils';
import { addMapControlTooltip } from '../map-control-tooltip/map-control-tooltip';

const ZOOM_IN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M11 13H5V11H11V5H13V11H19V13H13V19H11V13Z" fill="currentColor"/>
</svg>`;

const ZOOM_OUT_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M5 11H19V13H5V11Z" fill="currentColor"/>
</svg>`;

export interface ZoomControlLabels {
  zoomInTipLabel: string;
  zoomOutTipLabel: string;
  zoomInTooltip: string;
  zoomOutTooltip: string;
}

export class ArtskartZoomControl extends Zoom {
  private readonly zoomInButton: HTMLButtonElement;
  private readonly zoomOutButton: HTMLButtonElement;
  private readonly zoomInTooltip: HTMLSpanElement;
  private readonly zoomOutTooltip: HTMLSpanElement;

  constructor(labels: ZoomControlLabels, target?: HTMLElement) {
    super({
      className: 'artskart-zoom',
      zoomInClassName: 'artskart-zoom-in',
      zoomOutClassName: 'artskart-zoom-out',
      zoomInLabel: createSvgIcon(ZOOM_IN_SVG),
      zoomOutLabel: createSvgIcon(ZOOM_OUT_SVG),
      zoomInTipLabel: labels.zoomInTipLabel,
      zoomOutTipLabel: labels.zoomOutTipLabel,
      target,
    });
    [this.zoomInButton, this.zoomOutButton] = this.element.querySelectorAll('button');
    this.zoomInTooltip = addMapControlTooltip(this.zoomInButton, labels.zoomInTooltip);
    this.zoomOutTooltip = addMapControlTooltip(this.zoomOutButton, labels.zoomOutTooltip);
    // OL sets title from tipLabel — clear it and use aria-label instead
    this.updateLabels(labels);
  }

  updateLabels(labels: ZoomControlLabels): void {
    this.zoomInButton.title = '';
    this.zoomInButton.setAttribute('aria-label', labels.zoomInTipLabel);
    this.zoomInTooltip.textContent = labels.zoomInTooltip;
    this.zoomOutButton.title = '';
    this.zoomOutButton.setAttribute('aria-label', labels.zoomOutTipLabel);
    this.zoomOutTooltip.textContent = labels.zoomOutTooltip;
  }
}
