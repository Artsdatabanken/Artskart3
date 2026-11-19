import FullScreen from 'ol/control/FullScreen';
import { createSvgIcon } from './control-utils';
import { addMapControlTooltip } from '../map-control-tooltip/map-control-tooltip';

const FULLSCREEN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M5 19V13H7V17H11V19H5ZM17 11V7H13V5H19V11H17Z" fill="currentColor"/>
</svg>`;

const EXIT_FULLSCREEN_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M9 19V15H5V13H11V19H9ZM13 11V5H15V9H19V11H13Z" fill="currentColor"/>
</svg>`;

export interface FullscreenControlLabels {
  tipLabel: string;
  tooltip: string;
  exitTooltip: string;
}

export class ArtskartFullscreenControl extends FullScreen {
  private readonly button: HTMLButtonElement;
  private readonly tooltip: HTMLSpanElement;
  private labels: FullscreenControlLabels;
  private fullscreen = false;

  constructor(labels: FullscreenControlLabels, { source, target }: { source?: HTMLElement; target?: HTMLElement } = {}) {
    super({
      className: 'artskart-fullscreen',
      label: createSvgIcon(FULLSCREEN_SVG),
      labelActive: createSvgIcon(EXIT_FULLSCREEN_SVG),
      tipLabel: labels.tipLabel,
      source,
      target,
    });
    [this.button] = this.element.querySelectorAll('button');
    this.labels = labels;
    this.tooltip = addMapControlTooltip(this.button, labels.tooltip);
    this.on('enterfullscreen', () => this.setFullscreen(true));
    this.on('leavefullscreen', () => this.setFullscreen(false));
    this.updateLabels(labels);
  }

  updateLabels(labels: FullscreenControlLabels): void {
    this.labels = labels;
    this.renderState();
  }

  private setFullscreen(fullscreen: boolean): void {
    this.fullscreen = fullscreen;
    this.renderState();
  }

  /** A toggle button: the name stays the same while aria-pressed and the tooltip follow the state. */
  private renderState(): void {
    this.button.title = '';
    this.button.setAttribute('aria-label', this.labels.tipLabel);
    this.button.setAttribute('aria-pressed', String(this.fullscreen));
    this.tooltip.textContent = this.fullscreen ? this.labels.exitTooltip : this.labels.tooltip;
  }
}
