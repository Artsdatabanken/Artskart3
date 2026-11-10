import { MapLayerDto } from '@shared/types/api.types';

export type MapToolbarMenu = 'mapTypes' | 'mapLayers' | 'mapSettings';

/** Why clustering can't be turned off right now. */
export type ClusteringLock = 'unknownCount' | 'tooManyLocations';

export interface MapToolbarMenuItemChange {
  layer: MapLayerDto;
  visible: boolean;
}
