import { Component, Output, EventEmitter, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MAP_TYPE_OPTIONS } from '../../../../config/map/map-layer.config';

@Component({
  selector: 'app-map-type-selector',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-type-selector.component.html',
  styleUrl: './map-type-selector.component.css',
})
export class MapTypeSelectorComponent {
  @Output() mapTypeSelected = new EventEmitter<string>();

  readonly mapTypeOptions = MAP_TYPE_OPTIONS;
  isMapTypesOpen = false;
  selectedLayerId = 'topografiskBaseLayer';

  private readonly translate = inject(TranslateService);

  toggleMapTypes(): void {
    this.isMapTypesOpen = !this.isMapTypesOpen;
  }

  selectMapType(layerId: string): void {
    this.selectedLayerId = layerId;
    this.mapTypeSelected.emit(layerId);
    this.isMapTypesOpen = false;
  }

  onRadioGroupChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.selectMapType(target.value);
    }
  }

  getToggleButtonAriaLabel(): string {
    const selectedLabel = this.mapTypeOptions.find(opt => opt.layerId === this.selectedLayerId)?.label || '';
    return this.translate.instant('mapToolbar.selectMapTypeAriaLabel', { mapType: selectedLabel });
  }
}
