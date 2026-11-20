import Zoom from 'ol/control/Zoom';
import { iconAdd, iconRemove } from '@artsdatabanken/icons/svg';
import { createSvgIcon } from './control-utils';
import { addMapControlTooltip } from '../map-control-tooltip/map-control-tooltip';

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
      zoomInLabel: createSvgIcon(iconAdd),
      zoomOutLabel: createSvgIcon(iconRemove),
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
