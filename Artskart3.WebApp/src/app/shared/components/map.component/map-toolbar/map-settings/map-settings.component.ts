import { Component, CUSTOM_ELEMENTS_SCHEMA, input, model, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-map-settings',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-settings.component.html',
  styleUrl: './map-settings.component.css',
})
export class MapSettingsComponent {
  readonly isOpen = input(false);
  readonly mapSettingsOpenChange = output<boolean>();
  readonly clusteringEnabled = model(true);

  toggleMapSettings(): void {
    this.mapSettingsOpenChange.emit(!this.isOpen());
  }

  onClusteringChange(event: Event): void {
    this.clusteringEnabled.set(Boolean((event.target as HTMLElement & { checked?: boolean }).checked));
  }
}
