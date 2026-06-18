export interface MapTypeOption {
  label: string;
  layerId: string;
}

export const MAP_TYPE_OPTIONS: MapTypeOption[] = [
  { label: 'Standard', layerId: 'osm' },
  { label: 'Landkart', layerId: 'topografisk' },
  { label: 'Gråtonekart', layerId: 'topo4graatone' },
  { label: 'Norge i bilder', layerId: 'nib' },
  { label: 'Svalbard', layerId: 'svalbard' },
  { label: 'Jan Mayen', layerId: 'janmayen' },
];
