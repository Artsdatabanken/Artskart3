import Control from 'ol/control/Control';
import { iconMyLocation } from '@artsdatabanken/icons/svg';
import { addMapControlTooltip, IMMEDIATE_TOOLTIP_CLASS } from '../map-control-tooltip/map-control-tooltip';
import { createSvgIcon } from './control-utils';

export interface GeolocationControlLabels {
  tipLabel: string;
  tooltip: string;
  deniedTooltip: string;
}

export interface GeolocationControlCallbacks {
  onClick: () => Promise<unknown>;
}

export class GeolocationMapControl extends Control {
  private button: HTMLButtonElement;
  private tooltip: HTMLSpanElement;
  private labels: GeolocationControlLabels;
  private denied = false;
  private permissionStatus: PermissionStatus | null = null;

  constructor(
    labels: GeolocationControlLabels,
    private callbacks: GeolocationControlCallbacks,
    target?: HTMLElement,
  ) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'artskart-geolocation-btn';
    button.appendChild(createSvgIcon(iconMyLocation));
    button.setAttribute('aria-label', labels.tipLabel);

    const element = document.createElement('div');
    element.className = 'artskart-geolocation ol-unselectable ol-control';
    element.appendChild(button);
    const tooltip = addMapControlTooltip(button, labels.tooltip);

    super({ element, target });
    this.button = button;
    this.tooltip = tooltip;
    this.labels = labels;

    button.addEventListener('click', () => this.handleClick());

    this.queryPermission();
  }

  override dispose(): void {
    if (this.permissionStatus) {
      this.permissionStatus.onchange = null;
      this.permissionStatus = null;
    }
    super.dispose();
  }

  setDenied(denied: boolean): void {
    this.denied = denied;
    this.button.ariaDisabled = denied.toString();
    this.renderTooltip();
  }

  updateLabels(labels: GeolocationControlLabels): void {
    this.labels = labels;
    this.button.setAttribute('aria-label', labels.tipLabel);
    this.renderTooltip();
  }

  /** The denied explanation replaces the normal tooltip and shows without the hover delay. */
  private renderTooltip(): void {
    this.tooltip.textContent = this.denied ? this.labels.deniedTooltip : this.labels.tooltip;
    this.tooltip.classList.toggle(IMMEDIATE_TOOLTIP_CLASS, this.denied);
  }

  private async handleClick(): Promise<void> {
    if (this.denied) return;

    const permissionGranted = await new Promise<boolean>((resolve) => {
      navigator.geolocation.getCurrentPosition(
        () => resolve(true),
        (error) => resolve(error.code !== 1),
        { timeout: 5000, maximumAge: Infinity },
      );
    });

    if (!permissionGranted) {
      this.setDenied(true);
      return;
    }

    try {
      await this.callbacks.onClick();
    } catch (error) {
      // Map zoom failed but permission was granted — don't disable button
      console.error("Map zoom failed but permission was granted — don't disable button", error);
    }
  }

  private queryPermission(): void {
    if (!navigator.permissions) return;

    navigator.permissions
      .query({ name: 'geolocation' })
      .then((status) => {
        this.permissionStatus = status;
        if (status.state === 'denied') {
          this.setDenied(true);
        }
        status.onchange = () => this.setDenied(status.state === 'denied');
      })
      .catch(() => {
        // Permissions API not supported for geolocation in this browser
        console.warn('Permissions API not supported for geolocation in this browser');
      });
  }
}

export function createGeolocationControl(
  labels: GeolocationControlLabels,
  callbacks: GeolocationControlCallbacks,
  target?: HTMLElement,
): GeolocationMapControl {
  return new GeolocationMapControl(labels, callbacks, target);
}
