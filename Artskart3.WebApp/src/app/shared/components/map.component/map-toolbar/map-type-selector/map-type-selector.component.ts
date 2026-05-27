import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAP_TYPE_OPTIONS } from '../../../../config/map/map-layer.config';

@Component({
  selector: 'app-map-type-selector',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './map-type-selector.component.html',
  styleUrl: './map-type-selector.component.css',
})
export class MapTypeSelectorComponent {
  @Output() mapTypeSelected = new EventEmitter<string>();

  readonly mapTypeOptions = MAP_TYPE_OPTIONS;
  isMapTypesOpen = false;
  selectedLayerId = 'topografiskBaseLayer';

  toggleMapTypes(): void {
    this.isMapTypesOpen = !this.isMapTypesOpen;
  }

  selectMapType(layerId: string): void {
    this.selectedLayerId = layerId;
    this.mapTypeSelected.emit(layerId);
    this.isMapTypesOpen = false;
  }

  getToggleButtonAriaLabel(): string {
    const selectedLabel = this.mapTypeOptions.find(opt => opt.layerId === this.selectedLayerId)?.label || '';
    return `Velg karttype - ${selectedLabel}`;
  }
}
