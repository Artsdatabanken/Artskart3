export interface MapTypeOption {
  label: string;
  layerId: string;
}

export const MAP_TYPE_OPTIONS: MapTypeOption[] = [
  { label: 'mapToolbar.mapTypes.topografisk', layerId: 'topografisk' },
  { label: 'mapToolbar.mapTypes.topo4graatone', layerId: 'topo4graatone' },
  { label: 'mapToolbar.mapTypes.nib', layerId: 'nib' },
  { label: 'mapToolbar.mapTypes.svalbard', layerId: 'svalbard' },
  { label: 'mapToolbar.mapTypes.janmayen', layerId: 'janmayen' },
];
