import { Component, CUSTOM_ELEMENTS_SCHEMA, input } from '@angular/core';
import { RegistrationStatus } from '@shared/helpers/registration-status/registration-status';

@Component({
  selector: 'app-registration-marker',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  styleUrl: './registration-marker.component.css',
  host: {
    '[class.found]': "status() === 'found'",
    '[class.negative]': "status() !== 'found'",
    '[attr.role]': 'label() ? "img" : null',
    '[attr.aria-label]': 'label() || null',
    '[attr.aria-hidden]': 'label() ? null : "true"',
  },
  template: `
    @if (status() !== 'found') {
      <adb-icon [attr.name]="status() === 'notRecovered' ? 'remove' : 'close'" size="sm" />
    }
  `,
})
export class RegistrationMarkerComponent {
  readonly status = input.required<RegistrationStatus>();
  /** Without a label the marker is decorative, for use next to a visible status text. */
  readonly label = input('');
}
