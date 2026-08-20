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
  wktsPolygon?: string;
  centroid?: { x: number; y: number };
}

export interface AreaMarkerFeature {
  type: 'Feature';
  id?: number | string;
  geometry: {
    type: 'Polygon' | 'MultiPolygon' | 'Point';
    coordinates: number[][][] | number[][][][] | number[];
  };
  properties: {
    id: number;
    name: string;
    areaTypeId?: number;
    areaTypeName?: string;
    observationCount: number | null;
    observationCountDisplay: string;
    fid?: string;
    isPolygon: boolean;
    'nbic:style'?: {
      strokeColor: string;
      fillColor: string;
      strokeWidth: number;
      circle?: {
        radius: number;
        fillColor: string;
        strokeColor: string;
        strokeWidth: number;
      };
    };
    [key: string]: unknown;
  };
}

/**
 * Speiler Artskart3.Core/Domain/Enums/AreaType.cs
 */
export enum AreaTypeId {
  Municipality = 1,
  County = 2,
  RestrictedArea = 3,
  OceanArea = 4,
  NorwaysMarineAreas = 5,
  SvalbardBjørnøyaAndJanMayen = 6,
}

export const AREA_TYPE_CONFIG: Record<number, {
  id: number;
  name: string;
  color: string;
}> = {
  1: {
    id: 1,
    name: 'Municipality',
    color: '#005A71'
  },
  2: {
    id: 2,
    name: 'County',
    color: '#CC0000'
  }
};

export function getAreaTypeName(areaTypeId: number): string {
  return AREA_TYPE_CONFIG[areaTypeId]?.name ?? `Unknown (${areaTypeId})`;
}

export function getAreaTypeColor(areaTypeId: number): string {
  return AREA_TYPE_CONFIG[areaTypeId]?.color ?? '#005A71';
}

export interface LocationPolygonDto {
  locationId: number;
  locality?: string;
  wktPolygon: string;
  observationCount: number;
}
