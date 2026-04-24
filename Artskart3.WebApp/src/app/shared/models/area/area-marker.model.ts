export interface AreaMarkerDto {
  $id?: string;
  id: number;
  documentId: string;
  fid: string;
  name: string;
  areaTypeId: number;
  parentFid: string;
  syncDateTime: string;
  observationCount?: number;
  timeStamp: string;
  isCurrent: boolean;
  centroid?: string;
}

export interface AreaMarker extends AreaMarkerDto {
  coordinates?: [number, number];
  properties?: {
    title: string;
    description: string;
    observationCount: number | null;
    areaTypeName: string;
  };
}

export interface AreaMarkerFeature {
  type: 'Feature';
  geometry: {
    type: 'Point';
    coordinates: [number, number];
  };
  properties: {
    id: number;
    name: string;
    areaTypeId: number;
    areaTypeName: string;
    observationCount: number | null;
    observationCountDisplay: string;
    fid: string;
  };
}

export interface ZoomLevelConfig {
  minZoom: number;
  maxZoom: number;
  visibleAreaTypes: number[];
  clusteringEnabled: boolean;
  markerSize: 'small' | 'medium' | 'large';
}

export const AREA_TYPE_CONFIG: Record<number, {
  id: number;
  name: string;
  zoomLevels: ZoomLevelConfig;
  color: string;
}> = {
  1: {
    id: 1,
    name: 'Municipality',
    zoomLevels: {
      minZoom: 9,
      maxZoom: 12,
      visibleAreaTypes: [1],
      clusteringEnabled: false,
      markerSize: 'medium'
    },
    color: '#005A71'
  },
  2: {
    id: 2,
    name: 'County',
    zoomLevels: {
      minZoom: 4,
      maxZoom: 8,
      visibleAreaTypes: [2], // Todo: for now hard coded to only show county at these zoom levels, but ideally should be dynamic based on config
      clusteringEnabled: false,
      markerSize: 'large'
    },
    color: '#005A71'
  }
};

export function getAreaTypeName(areaTypeId: number): string {
  return AREA_TYPE_CONFIG[areaTypeId]?.name ?? `Unknown (${areaTypeId})`;
}


export function getAreaTypeColor(areaTypeId: number): string {
  return AREA_TYPE_CONFIG[areaTypeId]?.color ?? '#005A71';
}

export function isAreaTypeVisibleAtZoom(areaTypeId: number, zoomLevel: number): boolean {
  const config = AREA_TYPE_CONFIG[areaTypeId];
  if (!config) return false;
  return zoomLevel >= config.zoomLevels.minZoom && zoomLevel <= config.zoomLevels.maxZoom;
}
