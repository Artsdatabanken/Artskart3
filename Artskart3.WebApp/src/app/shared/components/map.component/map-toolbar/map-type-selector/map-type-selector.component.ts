import { Component, Output, EventEmitter, CUSTOM_ELEMENTS_SCHEMA, ElementRef, inject, Input, viewChild } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MAP_TYPE_OPTIONS } from '../../../../config/map/map-layer.config';
import { MapControlTooltipDirective } from '../../map-control-tooltip/map-control-tooltip.directive';

@Component({
  selector: 'app-map-type-selector',
  imports: [TranslateModule, MapControlTooltipDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-type-selector.component.html',
  styleUrl: './map-type-selector.component.css',
})
export class MapTypeSelectorComponent {
  @Input() isOpen = false;
  @Output() mapTypeSelected = new EventEmitter<string>();
  @Output() mapTypesOpenChange = new EventEmitter<boolean>();

  readonly mapTypeOptions = MAP_TYPE_OPTIONS;
  selectedLayerId = 'topografisk';

  private readonly translate = inject(TranslateService);
  private readonly toggleButton = viewChild.required<ElementRef<HTMLElement>>('toggleButton');

  toggleMapTypes(): void {
    this.mapTypesOpenChange.emit(!this.isOpen);
  }

  selectMapType(layerId: string): void {
    this.selectedLayerId = layerId;
    this.mapTypeSelected.emit(layerId);
    this.mapTypesOpenChange.emit(false);
  }

  onRadioGroupChange(event: Event): void {
    const target = event.target as HTMLElement & { value: string };
    if (target.value) {
      this.selectMapType(target.value);
      // The menu closes and takes the focused radio with it, so focus returns to the toggle
      this.toggleButton().nativeElement.shadowRoot?.querySelector('button')?.focus();
    }
  }

  getToggleButtonAriaLabel(): string {
    const selectedKey = this.mapTypeOptions.find((opt) => opt.layerId === this.selectedLayerId)?.label || '';
    const selectedLabel = this.translate.instant(selectedKey);
    return this.translate.instant('mapToolbar.selectMapTypeAriaLabel', { mapType: selectedLabel });
  }
}
