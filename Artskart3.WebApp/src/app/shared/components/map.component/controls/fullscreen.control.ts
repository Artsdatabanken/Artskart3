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

const PSEUDO_FULLSCREEN_CLASS = 'map-pseudo-fullscreen';

// OpenLayers setter denne klassen når nettleseren mangler Fullscreen API (f.eks. iPhone)
const OL_UNSUPPORTED_CLASS = 'ol-unsupported';

export class ArtskartFullscreenControl extends FullScreen {
  private readonly pseudoTarget?: HTMLElement;

  constructor(labels: FullscreenControlLabels, source?: HTMLElement) {
    super({
      className: 'artskart-fullscreen',
      label: createSvgIcon(FULLSCREEN_SVG),
      labelActive: createSvgIcon(EXIT_FULLSCREEN_SVG),
      tipLabel: labels.tipLabel,
      source,
    });
    this.pseudoTarget = source;
    this.element.querySelector('button')?.addEventListener('click', () => this.togglePseudoFullscreen());
    this.updateLabels(labels);
  }

  // Fallback når Fullscreen API mangler: kartbeholderen fyller viewporten via CSS i stedet.
  private togglePseudoFullscreen(): void {
    if (!this.pseudoTarget || !this.element.classList.contains(OL_UNSUPPORTED_CLASS)) return;
    const active = this.pseudoTarget.classList.toggle(PSEUDO_FULLSCREEN_CLASS);
    this.element.querySelector('button')?.replaceChildren(createSvgIcon(active ? EXIT_FULLSCREEN_SVG : FULLSCREEN_SVG));
    this.getMap()?.updateSize();
  }

  updateLabels(labels: FullscreenControlLabels): void {
    const button = this.element.querySelector('button');
    if (button) {
      button.title = '';
      button.setAttribute('aria-label', labels.tipLabel);
    }
  }
}
