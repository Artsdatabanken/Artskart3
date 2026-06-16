export * from './areas.service';
export * from './area-map-layer.service';

export type {
  AreaMarkerDto,
  AreaMarkerFeature
} from '@shared/models/area/area-marker.model';

export {
  AREA_TYPE_CONFIG,
  getAreaTypeName,
  getAreaTypeColor
} from '@shared/models/area/area-marker.model';
