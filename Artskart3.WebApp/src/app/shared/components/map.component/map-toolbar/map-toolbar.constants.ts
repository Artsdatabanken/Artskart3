import { MapLayerDto } from '@shared/types/map-layer.types';

export type MapToolbarMenu = 'mapTypes' | 'mapLayers' | 'mapSettings';

export interface MapToolbarMenuItemChange {
  layer: MapLayerDto;
  visible: boolean;
}
