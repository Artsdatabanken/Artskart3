import { Component, CUSTOM_ELEMENTS_SCHEMA, computed, input, linkedSignal, model } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MAP_LAYER_LABELS } from '@shared/config/map/map-layer-labels.config';
import { WmsLegendImage, wmsLegendImages } from '@shared/helpers/map-legend/wms-legend';
import { MapLayerDto } from '@shared/types/api.types';
import {
  BORDER_WIDTH,
  CLUSTER_MARKER_RADIUS,
  POLYGON_ICON_VIEWBOX_SIZE,
  POLYGON_MARKER_SIZE,
  UNCLUSTERED_LOCATION_RADIUS,
  negativeGlyphPath,
  polygonIconBorderWidth,
  polygonIconPath,
} from '../../map-feature-styles';
import { MapControlTooltipDirective } from '../../map-control-tooltip/map-control-tooltip.directive';

interface LayerLegend {
  layer: MapLayerDto;
  images: WmsLegendImage[];
}

const SYMBOL_SIZE = 36;
const CENTER = SYMBOL_SIZE / 2;
// De små markørene står i samme venstrekant som clustermarkøren
const LOCATION_CX = CENTER - CLUSTER_MARKER_RADIUS + UNCLUSTERED_LOCATION_RADIUS;
const POLYGON_OFFSET = (SYMBOL_SIZE - POLYGON_MARKER_SIZE) / 2;

@Component({
  selector: 'app-map-legend',
  imports: [TranslateModule, MapControlTooltipDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-legend.component.html',
  styleUrl: './map-legend.component.css',
})
export class MapLegendComponent {
  readonly isOpen = model(false);
  readonly activeLayers = input<readonly MapLayerDto[]>([]);
  readonly geolocationActive = input(false);

  // Symbolene tegnes etter de samme målene som markørene i kartet
  protected readonly viewBox = `0 0 ${SYMBOL_SIZE} ${SYMBOL_SIZE}`;
  protected readonly center = CENTER;
  protected readonly locationCx = LOCATION_CX;
  protected readonly locationRadius = UNCLUSTERED_LOCATION_RADIUS;
  protected readonly clusterRadius = CLUSTER_MARKER_RADIUS;
  protected readonly borderWidth = BORDER_WIDTH;
  protected readonly absentGlyph = negativeGlyphPath('absent', UNCLUSTERED_LOCATION_RADIUS, [LOCATION_CX, CENTER]);
  protected readonly notRecoveredGlyph = negativeGlyphPath('notRecovered', UNCLUSTERED_LOCATION_RADIUS, [LOCATION_CX, CENTER]);
  protected readonly polygonPath = polygonIconPath();
  protected readonly polygonBorderWidth = polygonIconBorderWidth(POLYGON_MARKER_SIZE);
  protected readonly polygonTransform = `translate(${POLYGON_OFFSET} ${POLYGON_OFFSET}) scale(${POLYGON_MARKER_SIZE / POLYGON_ICON_VIEWBOX_SIZE})`;

  protected readonly mapLayerLabels: Partial<Record<string, string>> = MAP_LAYER_LABELS;
  private readonly failedImageUrls = linkedSignal<
    { layerIds: readonly number[]; open: boolean },
    ReadonlyMap<number, ReadonlySet<string>>
  >({
    source: () => ({ layerIds: this.activeLayers().map((layer) => layer.id), open: this.isOpen() }),
    computation: ({ layerIds, open }, previous) => {
      if (open && !previous?.source.open) return new Map();
      const failures = new Map(previous?.value ?? []);
      for (const layerId of failures.keys()) {
        if (!layerIds.includes(layerId)) failures.delete(layerId);
      }
      return failures;
    },
  });

  protected readonly layerLegends = computed<LayerLegend[]>(() => {
    const failedImageUrls = this.failedImageUrls();
    return this.activeLayers()
      .map((layer) => ({
        layer,
        images: wmsLegendImages(layer).filter((image) => !failedImageUrls.get(layer.id)?.has(image.url)),
      }))
      .filter(({ images }) => images.length > 0);
  });

  toggleMapLegend(): void {
    this.isOpen.update((open) => !open);
  }

  protected onImageError(layerId: number, url: string): void {
    this.failedImageUrls.update((failures) => {
      const next = new Map(failures);
      next.set(layerId, new Set(next.get(layerId)).add(url));
      return next;
    });
  }
}
