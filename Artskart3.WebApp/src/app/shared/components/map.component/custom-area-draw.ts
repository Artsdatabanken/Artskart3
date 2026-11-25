import { MapEvents, NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import type { Feature } from 'ol';
import GeoJSON from 'ol/format/GeoJSON';
import WKT from 'ol/format/WKT';
import { Polygon, type Geometry } from 'ol/geom';
import type { CustomAreaDrawingCommand } from '@shared/services/custom-area-drawing/custom-area-drawing.service';

/** The projection the API expects the custom area in. */
const CUSTOM_AREA_PROJECTION = 'EPSG:25833';

type DrawStyle = NonNullable<Parameters<NbicMapComponent['startDrawing']>[0]['style']>;

export interface CustomAreaStyles {
  normal: DrawStyle;
  /** For a polygon covering too many locations to be used. */
  rejected: DrawStyle;
}

export interface CustomAreaDrawCallbacks {
  /** A polygon was finished, or its corners were moved. */
  areaChanged(wkt: string): void;
  pointCountChanged(count: number): void;
  /** Drawing ended, with or without a new polygon. */
  drawingStopped(): void;
}

/**
 * Draws the custom area with nbic-map-component's draw API. The draw layer holds only one
 * polygon, either the one in the filter or a rejected one, and its corners can be dragged once
 * it is finished.
 */
export class CustomAreaDraw {
  private readonly wkt = new WKT();
  private readonly geoJson = new GeoJSON();
  private readonly unsubscribe: (() => void)[];
  private shownWkt: string | null = null;
  private shownRejected = false;
  private drawing = false;

  // nbic-map-component cancels the sketch on Escape without an event, so the corner count is reset here.
  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape' && this.drawing) this.callbacks.pointCountChanged(0);
  };

  constructor(
    private readonly map: NbicMapComponent,
    private readonly viewProjection: () => string,
    private readonly styles: CustomAreaStyles,
    private readonly callbacks: CustomAreaDrawCallbacks,
  ) {
    // The first corner starts the sketch. Each event's index counts the pointer as a corner too.
    this.unsubscribe = [
      map.on(MapEvents.DrawStart, () => callbacks.pointCountChanged(1)),
      map.on(MapEvents.DrawVertex, ({ index }) => callbacks.pointCountChanged(index)),
      map.on(MapEvents.DrawVertexRemoved, ({ index }) => callbacks.pointCountChanged(index - 1)),
      // OpenLayers adds the drawn feature to the layer after this event, so it is handled afterwards.
      map.on(MapEvents.DrawEnd, ({ feature }) => queueMicrotask(() => this.onDrawEnd(feature))),
      map.on(MapEvents.EditModified, ({ feature }) => this.onModified(feature)),
    ];
  }

  run(command: CustomAreaDrawingCommand): void {
    switch (command) {
      case 'start':
        this.drawing = true;
        window.addEventListener('keydown', this.onKeyDown);
        this.map.disableDrawEditing();
        this.map.startDrawing({ kind: 'Polygon', style: this.styles.normal, snap: false });
        break;
      case 'finish':
        this.map.finishCurrent();
        break;
      case 'undo':
        this.map.undoLastPoint();
        break;
      case 'cancel':
        this.map.abortCurrent();
        this.stopDrawing();
        this.render(this.shownWkt, this.shownRejected);
        break;
    }
  }

  /** Shows the polygon, or removes it. */
  show(wkt: string | null, rejected = false): void {
    if (wkt !== this.shownWkt || rejected !== this.shownRejected) this.render(wkt, rejected);
  }

  dispose(): void {
    this.unsubscribe.forEach((unsubscribe) => unsubscribe());
    window.removeEventListener('keydown', this.onKeyDown);
  }

  private onDrawEnd(feature: Feature<Geometry>): void {
    const geometry = feature.getGeometry();
    // Enter finishes the sketch whatever the number of corners, and the API rejects fewer than three.
    if (!(geometry instanceof Polygon) || (geometry.getLinearRing(0)?.getCoordinates().length ?? 0) < 4) {
      this.render(this.shownWkt, this.shownRejected);
      this.callbacks.pointCountChanged(0);
      return;
    }

    const wkt = this.toWkt(geometry);
    this.stopDrawing();
    this.render(wkt);
    this.callbacks.areaChanged(wkt);
  }

  private onModified(feature: Feature<Geometry>): void {
    const geometry = feature.getGeometry();
    if (!geometry) return;
    this.shownWkt = this.toWkt(geometry);
    this.callbacks.areaChanged(this.shownWkt);
  }

  private stopDrawing(): void {
    this.drawing = false;
    window.removeEventListener('keydown', this.onKeyDown);
    this.map.stopDrawing();
    this.callbacks.drawingStopped();
  }

  private render(wkt: string | null, rejected = false): void {
    this.shownWkt = wkt;
    this.shownRejected = wkt !== null && rejected;
    if (!wkt) {
      this.map.clearDrawn();
      if (!this.drawing) this.map.disableDrawEditing();
      return;
    }

    const projection = this.viewProjection();
    const feature = this.wkt.readFeature(wkt, { dataProjection: CUSTOM_AREA_PROJECTION, featureProjection: projection });
    const style = this.shownRejected ? this.styles.rejected : this.styles.normal;
    feature.set('nbic:style', style);
    // importDrawnGeoJSON doesn't reproject (it passes the view's projection object, not its code), so the
    // GeoJSON is written in the view's projection and says so. That also stays right once it does reproject.
    const geoJson = {
      ...this.geoJson.writeFeaturesObject([feature], { dataProjection: projection, featureProjection: projection }),
      crs: { type: 'name', properties: { name: projection } },
    };
    this.map.importDrawnGeoJSON(JSON.stringify(geoJson), { clearExisting: true });
    if (!this.drawing) this.map.enableDrawEditing({ showVertices: true, vertexStyle: { strokeColor: style.strokeColor } });
  }

  private toWkt(geometry: Geometry): string {
    return this.wkt.writeGeometry(geometry, {
      dataProjection: CUSTOM_AREA_PROJECTION,
      featureProjection: this.viewProjection(),
      decimals: 0,
    });
  }
}
