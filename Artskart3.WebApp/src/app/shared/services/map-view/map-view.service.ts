import { Service, signal } from '@angular/core';

/** [minX, minY, maxX, maxY] i kartets projeksjon (EPSG:25833). */
export type MapExtent = [number, number, number, number];

/**
 * Kartutsnittet, delt mellom kartet og resten av appen. Kartet skriver
 * `currentExtent` når det tegnes, og zoomer til `requestedExtent` så snart det
 * er klart og synlig — også om forespørselen kom før kartet fantes.
 */
@Service()
export class MapViewService {
  readonly currentExtent = signal<MapExtent | null>(null);
  readonly requestedExtent = signal<MapExtent | null>(null);

  requestFit(extent: MapExtent): void {
    this.requestedExtent.set(extent);
  }

  consumeRequestedExtent(): MapExtent | null {
    const extent = this.requestedExtent();
    this.requestedExtent.set(null);
    return extent;
  }
}
