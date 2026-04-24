export * from './areas.service';
export * from './area-map-layer.service';

export type {
  AreaMarkerDto,
  AreaMarker,
  AreaMarkerFeature,
  ZoomLevelConfig
} from '@shared/models/area/area-marker.model';

export {
  AREA_TYPE_CONFIG,
  getAreaTypeName,
  getAreaTypeColor,
  isAreaTypeVisibleAtZoom
} from '@shared/models/area/area-marker.model';
