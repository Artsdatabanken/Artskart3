import type { MapLayerDto } from '@shared/types/map-layer.types';
import { MAP_LAYER_LEGEND_LAYERS } from '@shared/config/map/map-layer-legend.config';

export interface WmsLegendImage {
  layerName: string;
  url: string;
}

/** Forklaringsbilder fra WMS-tjenesten, ett per WMS-lag. Andre korttyper har ingen forklaring. */
export function wmsLegendImages(layer: MapLayerDto): WmsLegendImage[] {
  if (layer.type.toLowerCase() !== 'wms' || !layer.url) return [];

  const layerNames = (MAP_LAYER_LEGEND_LAYERS[layer.name] ?? (layer.layers ?? '').split(','))
    .map((name) => name.trim())
    .filter(Boolean);

  return layerNames.flatMap((layerName) => {
    try {
      const url = new URL(layer.url);
      url.searchParams.set('SERVICE', 'WMS');
      url.searchParams.set('VERSION', layer.version ?? '1.3.0');
      url.searchParams.set('REQUEST', 'GetLegendGraphic');
      // MapServer (Geonorge og Artsdatabanken) avviser forespørselen uten SLD_VERSION
      url.searchParams.set('SLD_VERSION', '1.1.0');
      url.searchParams.set('FORMAT', layer.format ?? 'image/png');
      url.searchParams.set('LAYER', layerName);
      return [{ layerName, url: url.toString() }];
    } catch {
      return [];
    }
  });
}
