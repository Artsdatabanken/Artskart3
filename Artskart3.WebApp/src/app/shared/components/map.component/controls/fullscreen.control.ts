import FullScreen from 'ol/control/FullScreen';
import { CLASS_UNSUPPORTED } from 'ol/css';
import { iconCollapseContent, iconExpandContent } from '@artsdatabanken/icons/svg';
import { createSvgIcon } from './control-utils';
import { addMapControlTooltip } from '../map-control-tooltip/map-control-tooltip';

export interface FullscreenControlLabels {
  tipLabel: string;
  tooltip: string;
  exitTooltip: string;
}

const PSEUDO_FULLSCREEN_CLASS = 'map-pseudo-fullscreen';

export class ArtskartFullscreenControl extends FullScreen {
  private readonly button: HTMLButtonElement;
  private readonly tooltip: HTMLSpanElement;
  private readonly pseudoTarget?: HTMLElement;
  private labels: FullscreenControlLabels;
  private fullscreen = false;

  constructor(labels: FullscreenControlLabels, { source, target }: { source?: HTMLElement; target?: HTMLElement } = {}) {
    super({
      className: 'artskart-fullscreen',
      label: createSvgIcon(iconExpandContent),
      labelActive: createSvgIcon(iconCollapseContent),
      tipLabel: labels.tipLabel,
      source,
      target,
    });
    this.pseudoTarget = source;
    [this.button] = this.element.querySelectorAll('button');
    this.labels = labels;
    this.tooltip = addMapControlTooltip(this.button, labels.tooltip);
    this.on('enterfullscreen', () => this.setFullscreen(true));
    this.on('leavefullscreen', () => this.setFullscreen(false));
    this.button.addEventListener('click', () => this.togglePseudoFullscreen());
    this.updateLabels(labels);
  }

  // Fallback når Fullscreen API mangler: kartbeholderen fyller viewporten via CSS i stedet.
  private togglePseudoFullscreen(): void {
    if (!this.pseudoTarget || !this.element.classList.contains(CLASS_UNSUPPORTED)) return;
    const active = this.pseudoTarget.classList.toggle(PSEUDO_FULLSCREEN_CLASS);
    this.button.replaceChildren(createSvgIcon(active ? iconCollapseContent : iconExpandContent));
    this.dispatchEvent(active ? 'enterfullscreen' : 'leavefullscreen');
    this.getMap()?.updateSize();
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
