export interface MapLayerConfig {
  id: string;
  center: [number, number];
  zoom: number;
  visibleLayers: string[];
}

export interface MapTypeOption {
  label: string;
  layerId: string;
}

interface LayerDefinition {
  center: [number, number];
  zoom: number;
}

const DEFAULT_CENTER: [number, number] = [300000, 7220000];
const DEFAULT_ZOOM = 6.2;

const defaultLayerDefinition: LayerDefinition = {
  center: DEFAULT_CENTER,
  zoom: DEFAULT_ZOOM,
};

const LAYER_DEFINITIONS: Record<string, LayerDefinition> = {
  'osm': defaultLayerDefinition,
  'topografiskBaseLayer': defaultLayerDefinition,
  'svalbardBaseLayer': {
    center: [557420.638343, 8771386.791490],
    zoom: 7,
  },
  'janmayenBaseLayer': {
    center: [-349448.796222, 8048213.247184],
    zoom: 10,
  },
  'topo4graatoneBaseLayer': defaultLayerDefinition,
  'nib': defaultLayerDefinition,
};

export const MAP_TYPE_OPTIONS: MapTypeOption[] = [
  { label: 'Standard', layerId: 'osm' },
  { label: 'Landkart', layerId: 'topografiskBaseLayer' },
  { label: 'Gråtonekart', layerId: 'topo4graatoneBaseLayer' },
  { label: 'Norge i bildet', layerId: 'nib' },
  { label: 'Svalbard', layerId: 'svalbardBaseLayer' },
  { label: 'Jan Mayen', layerId: 'janmayenBaseLayer' },
];

export const MAP_LAYER_CONFIGS: Record<string, MapLayerConfig> = Object.entries(LAYER_DEFINITIONS).reduce(
  (acc, [id, def]) => ({
    ...acc,
    [id]: {
      id,
      ...def,
      visibleLayers: ['osm', id],
    },
  }),
  {}
);
