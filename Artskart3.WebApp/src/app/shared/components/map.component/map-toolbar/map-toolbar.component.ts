import { Component, CUSTOM_ELEMENTS_SCHEMA, model, output, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MapToolbarMenu, MapToolbarMenuItemChange } from './map-toolbar.constants';
import { MapTypeSelectorComponent } from './map-type-selector/map-type-selector.component';
import { MapLayersComponent } from './map-layers/map-layers.component';
import { MapSettingsComponent } from './map-settings/map-settings.component';

@Component({
  selector: 'app-map-toolbar',
  imports: [TranslateModule, MapTypeSelectorComponent, MapLayersComponent, MapSettingsComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  readonly iconClick = output<string>();
  readonly mapToolbarMenuItemChange = output<MapToolbarMenuItemChange>();
  readonly clusteringEnabled = model(true);

  protected readonly openMenu = signal<MapToolbarMenu | null>(null);

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
    this.mapToolbarMenuItemChange.emit(event);
  }

  private setMenuState(menu: MapToolbarMenu, isOpen: boolean): void {
    this.openMenu.set(isOpen ? menu : null);
  }
}
