import { Component, CUSTOM_ELEMENTS_SCHEMA, EventEmitter, Input, Output, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MapLayerService } from '@shared/services/map/map-layer.service';
import { MapLayerDto } from '@shared/types/api.types';
import { MAP_LAYER_LABELS } from '@shared/config/map/map-layer-labels.config';
import { MapControlTooltipDirective } from '../../map-control-tooltip/map-control-tooltip.directive';

@Component({
  selector: 'app-map-layers',
  imports: [TranslateModule, MapControlTooltipDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-layers.component.html',
  styleUrl: './map-layers.component.css',
})
export class MapLayersComponent {
  @Input() isOpen = false;
  @Output() mapLayersOpenChange = new EventEmitter<boolean>();
  @Output() mapLayerVisibilityChange = new EventEmitter<{ layer: MapLayerDto; visible: boolean }>();

  private readonly mapLayerService = inject(MapLayerService);
  readonly mapLayers = this.mapLayerService.mapLayers;
  readonly mapLayersLoadFailed = this.mapLayerService.mapLayersLoadFailed;
  readonly failedLayerIds = this.mapLayerService.failedLayerIds;
  readonly enabledLayerIds = this.mapLayerService.enabledLayerIds;
  readonly mapLayerLabels: Partial<Record<string, string>> = MAP_LAYER_LABELS;
  toggleMapLayers(): void {
    const opening = !this.isOpen;
    if (opening) this.mapLayerService.retryMapLayersLoad();
    this.mapLayersOpenChange.emit(opening);
  }

  isLayerEnabled(layerId: number): boolean {
    return this.enabledLayerIds().has(layerId);
  }

  onLayerChange(layer: MapLayerDto, event: Event): void {
    const visible = Boolean((event.target as HTMLElement & { checked?: boolean }).checked);
    this.mapLayerService.setLayerEnabled(layer.id, visible);
    this.mapLayerVisibilityChange.emit({ layer, visible });
  }
}
