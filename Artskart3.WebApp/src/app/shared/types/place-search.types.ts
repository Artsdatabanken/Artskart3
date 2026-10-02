export type PlaceSearchZoomCategory = 1 | 2 | 3;

export interface PlaceSearchResult {
  id: string;
  name: string;
  placeType: string;
  municipalities: string[];
  counties: string[];
  coordinates: [number, number];
  coordinateSystem: 'EPSG:25833';
  zoomLevel: PlaceSearchZoomCategory;
}