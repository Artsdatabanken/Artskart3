import { Service, signal } from '@angular/core';

/** [minX, minY, maxX, maxY] i kartets projeksjon (EPSG:25833). */
export type MapExtent = [number, number, number, number];

/** Halve bredden på det minste utsnittet rundt et punkt, så kartet ikke zoomer helt inn. */
const MIN_POINT_RADIUS_METERS = 5000;

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

  /** Zoomer til punktet med koordinatpresisjonen rundt, men aldri tettere enn 10 × 10 km. */
  requestFitPoint(east: number, north: number, precisionMeters?: number | null): void {
    const radius = Math.max(precisionMeters ?? 0, MIN_POINT_RADIUS_METERS);
    this.requestFit([east - radius, north - radius, east + radius, north + radius]);
  }

  consumeRequestedExtent(): MapExtent | null {
    const extent = this.requestedExtent();
    this.requestedExtent.set(null);
    return extent;
  }
}
