import FullScreen from 'ol/control/FullScreen';
import { createSvgIcon } from './control-utils';

const FULLSCREEN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M5 19V13H7V17H11V19H5ZM17 11V7H13V5H19V11H17Z" fill="currentColor"/>
</svg>`;

const EXIT_FULLSCREEN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M9 19V15H5V13H11V19H9ZM13 11V5H15V9H19V11H13Z" fill="currentColor"/>
</svg>`;

export interface FullscreenControlLabels {
  tipLabel: string;
}

export class ArtskartFullscreenControl extends FullScreen {
  constructor(labels: FullscreenControlLabels, source?: HTMLElement) {
    super({
      className: 'artskart-fullscreen',
      label: createSvgIcon(FULLSCREEN_SVG),
      labelActive: createSvgIcon(EXIT_FULLSCREEN_SVG),
      tipLabel: labels.tipLabel,
      source,
    });
    this.updateLabels(labels);
  }

  updateLabels(labels: FullscreenControlLabels): void {
    const button = this.element.querySelector('button');
    if (button) {
      button.title = '';
      button.setAttribute('aria-label', labels.tipLabel);
    }
  }
}
