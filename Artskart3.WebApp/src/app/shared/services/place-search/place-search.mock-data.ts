import { PlaceSearchResult } from '../../types/place-search.types';

export const PLACE_SEARCH_MOCK_DATA: PlaceSearchResult[] = [
  {
    id: 'alta-fjord-kvaenangen',
    name: 'Alta',
    placeType: 'Fjord',
    municipalities: ['Kvænangen'],
    counties: ['Troms'],
    coordinates: [768586.78, 7781425.05],
    coordinateSystem: 'EPSG:25833',
    zoomLevel: 1,
  },
  {
    id: 'alta-city',
    name: 'Alta',
    placeType: 'By',
    municipalities: ['Alta'],
    counties: ['Finnmark'],
    coordinates: [815288.9, 7783951.43],
    coordinateSystem: 'EPSG:25833',
    zoomLevel: 2,
  },
  {
    id: 'komsatoppen',
    name: 'Komsatoppen',
    placeType: 'Fjell',
    municipalities: ['Hammerfest'],
    counties: ['Finnmark'],
    coordinates: [820074.4, 7863550.66],
    coordinateSystem: 'EPSG:25833',
    zoomLevel: 3,
  },
];