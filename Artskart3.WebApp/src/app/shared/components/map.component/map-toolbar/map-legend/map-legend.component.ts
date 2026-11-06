import { Component, CUSTOM_ELEMENTS_SCHEMA, computed, input, model, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MAP_LAYER_LABELS } from '@shared/config/map/map-layer-labels.config';
import { WmsLegendImage, wmsLegendImages } from '@shared/helpers/map-legend/wms-legend';
import { MapLayerDto } from '@shared/types/map-layer.types';

interface LayerLegend {
  layer: MapLayerDto;
  images: WmsLegendImage[];
}

@Component({
  selector: 'app-map-legend',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-legend.component.html',
  styleUrl: './map-legend.component.css',
})
export class MapLegendComponent {
  readonly isOpen = model(false);
  readonly activeLayers = input<readonly MapLayerDto[]>([]);
  readonly geolocationActive = input(false);

  protected readonly mapLayerLabels: Partial<Record<string, string>> = MAP_LAYER_LABELS;
  private readonly failedImageUrls = signal<ReadonlySet<string>>(new Set());

  protected readonly layerLegends = computed<LayerLegend[]>(() =>
    this.activeLayers()
      .map((layer) => ({ layer, images: wmsLegendImages(layer).filter((image) => !this.failedImageUrls().has(image.url)) }))
      .filter(({ images }) => images.length > 0),
  );

  toggleMapLegend(): void {
    this.isOpen.update((open) => !open);
  }

  protected onImageError(url: string): void {
    this.failedImageUrls.update((urls) => new Set(urls).add(url));
  }
}
