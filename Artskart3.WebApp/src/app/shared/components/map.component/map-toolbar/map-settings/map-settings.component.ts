import { Component, CUSTOM_ELEMENTS_SCHEMA, inject, input, model, output } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ZoomConfig } from '@shared/helpers/zoom/zoom-config';
import { FormatNumberPipe } from '@shared/pipes/format-number.pipe';
import { ClusteringLock } from '../map-toolbar.constants';

@Component({
  selector: 'app-map-settings',
  imports: [TranslateModule, FormatNumberPipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-settings.component.html',
  styleUrl: './map-settings.component.css',
})
export class MapSettingsComponent {
  readonly isOpen = input(false);
  readonly mapSettingsOpenChange = output<boolean>();
  readonly clusteringEnabled = model(true);
  readonly clusteringLock = input<ClusteringLock | null>(null);

  protected readonly translate = inject(TranslateService);
  protected readonly maxLocations = ZoomConfig.MAX_LOCATIONS_PER_FETCH;

  toggleMapSettings(): void {
    this.mapSettingsOpenChange.emit(!this.isOpen());
  }

  onClusteringChange(event: Event): void {
    this.clusteringEnabled.set(Boolean((event.target as HTMLElement & { checked?: boolean }).checked));
  }
}
