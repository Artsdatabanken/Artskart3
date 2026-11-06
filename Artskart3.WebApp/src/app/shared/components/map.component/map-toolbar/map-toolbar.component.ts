import { Component, CUSTOM_ELEMENTS_SCHEMA, input, model, output, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ClusteringLock, MapToolbarMenu, MapToolbarMenuItemChange } from './map-toolbar.constants';
import { MapTypeSelectorComponent } from './map-type-selector/map-type-selector.component';
import { MapLayersComponent } from './map-layers/map-layers.component';
import { MapSettingsComponent } from './map-settings/map-settings.component';
import { MapLegendComponent } from './map-legend/map-legend.component';
import { MapLayerDto } from '@shared/types/map-layer.types';

@Component({
  selector: 'app-map-toolbar',
  imports: [TranslateModule, MapTypeSelectorComponent, MapLayersComponent, MapSettingsComponent, MapLegendComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  readonly iconClick = output<string>();
  readonly mapToolbarMenuItemChange = output<MapToolbarMenuItemChange>();
  readonly clusteringEnabled = model(true);
  readonly clusteringLock = input<ClusteringLock | null>(null);
  readonly geolocationActive = input(false);

  protected readonly openMenu = signal<MapToolbarMenu | null>(null);
  protected readonly activeLayers = signal<readonly MapLayerDto[]>([]);

  onButtonClick(iconName: string): void {
    this.iconClick.emit(iconName);
  }

  onMapTypeSelected(layerId: string): void {
    this.iconClick.emit(`map-type:${layerId}`);
  }

  onMapTypesOpenChange(isOpen: boolean): void {
    this.setMenuState('mapTypes', isOpen);
  }

  onMapLayersOpenChange(isOpen: boolean): void {
    this.setMenuState('mapLayers', isOpen);
  }

  onMapSettingsOpenChange(isOpen: boolean): void {
    this.setMenuState('mapSettings', isOpen);
  }

  onMapToolbarMenuItemChange(event: MapToolbarMenuItemChange): void {
    this.activeLayers.update((layers) => {
      const others = layers.filter((layer) => layer.id !== event.layer.id);
      return event.visible ? [...others, event.layer] : others;
    });
    this.mapToolbarMenuItemChange.emit(event);
  }

  private setMenuState(menu: MapToolbarMenu, isOpen: boolean): void {
    this.openMenu.set(isOpen ? menu : null);
  }
}
