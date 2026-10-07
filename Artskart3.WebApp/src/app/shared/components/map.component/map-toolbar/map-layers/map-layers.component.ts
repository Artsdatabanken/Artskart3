import { Component, CUSTOM_ELEMENTS_SCHEMA, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MapLayerService } from '@shared/services/map/map-layer.service';
import { MapLayerDto } from '@shared/types/map-layer.types';
import { MAP_LAYER_LABELS } from '@shared/config/map/map-layer-labels.config';

@Component({
  selector: 'app-map-layers',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-layers.component.html',
  styleUrl: './map-layers.component.css',
})
export class MapLayersComponent {
  @Input() isOpen = false;
  @Output() mapLayersOpenChange = new EventEmitter<boolean>();
  @Output() mapLayerVisibilityChange = new EventEmitter<{ layer: MapLayerDto; visible: boolean }>();

  readonly mapLayers = toSignal(inject(MapLayerService).getMapLayers(), { initialValue: [] });
  readonly enabledLayerIds = signal(new Set<number>());
  readonly mapLayerLabels = MAP_LAYER_LABELS;
  toggleMapLayers(): void {
    this.mapLayersOpenChange.emit(!this.isOpen);
  }

  isLayerEnabled(layerId: number): boolean {
    return this.enabledLayerIds().has(layerId);
  }

  onLayerChange(layer: MapLayerDto, event: Event): void {
    const visible = Boolean((event.target as HTMLElement & { checked?: boolean }).checked);
    const enabledLayerIds = new Set(this.enabledLayerIds());
    if (visible) {
      enabledLayerIds.add(layer.id);
    } else {
      enabledLayerIds.delete(layer.id);
    }
    this.enabledLayerIds.set(enabledLayerIds);
    this.mapLayerVisibilityChange.emit({ layer, visible });
  }
}
