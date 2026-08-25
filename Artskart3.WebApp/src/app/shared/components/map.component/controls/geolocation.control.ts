import Control from 'ol/control/Control';

const GEOLOCATION_SVG = `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
  <path d="M10.9998 22.95V20.95C8.91647 20.7167 7.12897 19.8542 5.6373 18.3625C4.14564 16.8709 3.28314 15.0834 3.0498 13H1.0498V11H3.0498C3.28314 8.91672 4.14564 7.12922 5.6373 5.63755C7.12897 4.14588 8.91647 3.28338 10.9998 3.05005V1.05005H12.9998V3.05005C15.0831 3.28338 16.8706 4.14588 18.3623 5.63755C19.854 7.12922 20.7165 8.91672 20.9498 11H22.9498V13H20.9498C20.7165 15.0834 19.854 16.8709 18.3623 18.3625C16.8706 19.8542 15.0831 20.7167 12.9998 20.95V22.95H10.9998ZM11.9998 19C13.9331 19 15.5831 18.3167 16.9498 16.95C18.3165 15.5834 18.9998 13.9334 18.9998 12C18.9998 10.0667 18.3165 8.41672 16.9498 7.05005C15.5831 5.68338 13.9331 5.00005 11.9998 5.00005C10.0665 5.00005 8.41647 5.68338 7.0498 7.05005C5.68314 8.41672 4.9998 10.0667 4.9998 12C4.9998 13.9334 5.68314 15.5834 7.0498 16.95C8.41647 18.3167 10.0665 19 11.9998 19ZM11.9998 16C10.8998 16 9.95814 15.6084 9.1748 14.825C8.39147 14.0417 7.9998 13.1 7.9998 12C7.9998 10.9 8.39147 9.95838 9.1748 9.17505C9.95814 8.39172 10.8998 8.00005 11.9998 8.00005C13.0998 8.00005 14.0415 8.39172 14.8248 9.17505C15.6081 9.95838 15.9998 10.9 15.9998 12C15.9998 13.1 15.6081 14.0417 14.8248 14.825C14.0415 15.6084 13.0998 16 11.9998 16ZM11.9998 14C12.5498 14 13.0206 13.8042 13.4123 13.4125C13.804 13.0209 13.9998 12.55 13.9998 12C13.9998 11.45 13.804 10.9792 13.4123 10.5875C13.0206 10.1959 12.5498 10 11.9998 10C11.4498 10 10.979 10.1959 10.5873 10.5875C10.1956 10.9792 9.9998 11.45 9.9998 12C9.9998 12.55 10.1956 13.0209 10.5873 13.4125C10.979 13.8042 11.4498 14 11.9998 14Z" fill="currentColor"/>
</svg>`;

export interface GeolocationControlLabels {
  tipLabel: string;
  deniedTooltip: string;
}

export interface GeolocationControlCallbacks {
  onClick: () => Promise<unknown>;
}

export class GeolocationMapControl extends Control {
  private button: HTMLButtonElement;
  private tooltip: HTMLSpanElement;
  private denied = false;
  private permissionStatus: PermissionStatus | null = null;
  private readonly tooltipId = `geolocation-denied-tooltip-${Math.random().toString(36).slice(2, 8)}`;

  constructor(
    labels: GeolocationControlLabels,
    private callbacks: GeolocationControlCallbacks,
  ) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'artskart-geolocation-btn';
    button.innerHTML = GEOLOCATION_SVG;
    button.setAttribute('aria-label', labels.tipLabel);

    const tooltip = document.createElement('span');
    tooltip.className = 'artskart-geolocation-tooltip';
    tooltip.setAttribute('role', 'tooltip');
    tooltip.textContent = labels.deniedTooltip;

    const wrapper = document.createElement('div');
    wrapper.className = 'artskart-geolocation-wrapper';
    wrapper.appendChild(button);
    wrapper.appendChild(tooltip);

    const element = document.createElement('div');
    element.className = 'artskart-geolocation ol-unselectable ol-control';
    element.appendChild(wrapper);

    super({ element });
    this.button = button;
    this.tooltip = tooltip;

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
    this.tooltip.classList.toggle('artskart-geolocation-tooltip--visible', denied);
    if (denied) {
      this.button.setAttribute('aria-describedby', this.tooltipId);
      this.tooltip.id = this.tooltipId;
    } else {
      this.button.removeAttribute('aria-describedby');
      this.tooltip.removeAttribute('id');
    }
  }

  updateLabels(labels: GeolocationControlLabels): void {
    this.button.setAttribute('aria-label', labels.tipLabel);
    this.tooltip.textContent = labels.deniedTooltip;
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

export function createGeolocationControl(labels: GeolocationControlLabels, callbacks: GeolocationControlCallbacks): GeolocationMapControl {
  return new GeolocationMapControl(labels, callbacks);
}
