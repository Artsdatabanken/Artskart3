export interface LayerConfig {
  readonly countiesLayerId: string;
  readonly municipalitiesLayerId: string;
  readonly locationPointsLayerId: string;
}

export enum AreaTypeId {
  LocationPoints = 0,
  Municipality = 1,
  County = 2
}

export enum ApiZoomLevel {
  RegionalCounties = 0,
  RegionalMunicipalities = 1,
  LocalMunicipalities = 2,
  LocationPoints = 3
}

export interface AreaTypeStyle {
  zIndex: number;
  fontSize: string;
}
