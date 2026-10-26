import { asArray, asString, type Color } from 'ol/color';
import type { Coordinate } from 'ol/coordinate';
import type { FeatureLike } from 'ol/Feature';
import LineString from 'ol/geom/LineString';
import Point from 'ol/geom/Point';
import { Circle as CircleStyle, Fill, Icon, Stroke, Style, Text } from 'ol/style';
import type { FlatStyle, Rule } from 'ol/style/flat';
import type { RenderFunction } from 'ol/style/Style';
import type { RegistrationStatus } from '@shared/helpers/registration-status/registration-status';

export type MapFeatureState = 'default' | 'hover' | 'selected';
export type MapMarkerKind = 'location' | 'unclusteredLocation' | 'cluster' | 'area' | 'observation';
export type PolygonMarkerKind = Extract<MapMarkerKind, 'location' | 'unclusteredLocation'>;

const BORDER_WIDTH = 1.5;
const LABEL_FONT = '600 10px "Chivo", sans-serif';
export const UNCLUSTERED_LOCATION_RADIUS = 6.4;
const MARKERS = {
  location: { radius: 8, font: undefined },
  unclusteredLocation: { radius: UNCLUSTERED_LOCATION_RADIUS, font: undefined },
  cluster: { radius: 17, font: LABEL_FONT },
  area: { radius: 20, font: LABEL_FONT },
  observation: { radius: 11, font: undefined },
} satisfies Record<MapMarkerKind, { radius: number; font: string | undefined }>;

const POLYGON_MARKER_SIZES = {
  location: markerSize(MARKERS.cluster.radius),
  unclusteredLocation: 20,
} satisfies Record<PolygonMarkerKind, number>;
const POLYGON_ICON_VIEWBOX_SIZE = 46;
const POLYGON_ICON_CORNERS: Coordinate[] = [
  [0, 5],
  [0, 34.5],
  [46, 46],
  [41, 0],
];

const SELECTED_RING_WIDTH = 5;
/** The space between a popped out marker's selected ring and its cluster, bridged by a line. */
const POP_OUT_GAP = 10;

const COLOR_TOKENS = {
  primary: '--adb-color-brand-freshwater-blue-90',
  hover: '--adb-color-brand-freshwater-blue-100',
  border: '--adb-color-neutrals-ocean-10',
  hoverRing: '--adb-color-neutrals-ocean-50',
  selectedRing: '--adb-color-brand-sun-70',
  negativeBorder: '--adb-color-special-100',
  negativeGlyph: '--adb-color-special-110',
} as const;

// Rasterized larger than drawn, so icons stay sharp on high-DPI screens.
const ICON_RASTER_SCALE = 4;

type MapPalette = Record<keyof typeof COLOR_TOKENS, Color>;

function readColor(styles: CSSStyleDeclaration, token: string): Color {
  const value = styles.getPropertyValue(token).trim();
  if (!value || value.includes('var(')) throw new Error(`Missing or unresolved map color token: ${token}`);
  try {
    return [...asArray(value)];
  } catch (cause: unknown) {
    throw new Error(`Invalid map color token: ${token} (${value})`, { cause });
  }
}

function tracePolygon(context: CanvasRenderingContext2D, rings: Coordinate[][]): void {
  for (const ring of rings) {
    if (!ring.length) continue;
    context.moveTo(ring[0][0], ring[0][1]);
    for (let i = 1; i < ring.length; i++) context.lineTo(ring[i][0], ring[i][1]);
    context.closePath();
  }
}

/** Strokes the outlines of the polygons, given in pixels, leaving out everything inside them. */
function strokeOutside(context: CanvasRenderingContext2D, polygons: Coordinate[][][], color: Color, lineWidth: number): void {
  context.save();
  // Intersect the complements of all parts, retaining holes but excluding the
  // entire filled union (also when MultiPolygon parts overlap).
  for (const rings of polygons) {
    context.beginPath();
    context.rect(0, 0, context.canvas.width, context.canvas.height);
    tracePolygon(context, rings);
    context.clip('evenodd');
  }
  context.beginPath();
  for (const rings of polygons) tracePolygon(context, rings);
  context.strokeStyle = asString(color);
  context.lineWidth = lineWidth;
  context.lineJoin = 'round';
  context.lineCap = 'round';
  context.stroke();
  context.restore();
}

function exteriorRingRenderer(color: Color, width: number): RenderFunction {
  return (coordinates, { context, geometry, pixelRatio }) => {
    // OL supplies pixel coordinates with the same nesting as the source geometry.
    const polygons = geometry.getType() === 'MultiPolygon' ? (coordinates as Coordinate[][][]) : [coordinates as Coordinate[][]];
    strokeOutside(context, polygons, color, (BORDER_WIDTH + width * 2) * pixelRatio);
  };
}

/** The icon's border lies inside its outline, so the ring starts right at the outline. */
function polygonIconRingRenderer(corners: Coordinate[], color: Color, width: number): RenderFunction {
  return (coordinates, { context, pixelRatio }) => {
    const [x, y] = coordinates as Coordinate;
    const outline = corners.map(([dx, dy]) => [x + dx * pixelRatio, y + dy * pixelRatio]);
    strokeOutside(context, [[outline]], color, width * 2 * pixelRatio);
  };
}

function circleRingRenderer(radius: number, color: Color, width: number): RenderFunction {
  return (coordinates, { context, pixelRatio }) => {
    const [x, y] = coordinates as Coordinate;
    context.save();
    context.beginPath();
    context.arc(x, y, (radius + BORDER_WIDTH / 2 + width / 2) * pixelRatio, 0, Math.PI * 2);
    context.strokeStyle = asString(color);
    context.lineWidth = width * pixelRatio;
    context.stroke();
    context.restore();
  };
}

function lineRenderer(color: Color): RenderFunction {
  return (coordinates, { context, pixelRatio }) => {
    const [[startX, startY], [endX, endY]] = coordinates as Coordinate[];
    context.save();
    context.beginPath();
    context.moveTo(startX, startY);
    context.lineTo(endX, endY);
    context.strokeStyle = asString(color);
    context.lineWidth = BORDER_WIDTH * pixelRatio;
    context.stroke();
    context.restore();
  };
}

export function registrationStatusOf(feature: FeatureLike): RegistrationStatus {
  const status: unknown = feature.get('registrationStatus');
  return status === 'absent' || status === 'notRecovered' ? status : 'found';
}

/** Locations drawn as polygons get the polygon marker, unless nothing was found there. */
export function hasPolygonMarker(feature: FeatureLike): boolean {
  return feature.get('isPolygon') === true && registrationStatusOf(feature) === 'found';
}

/** Width and height of a circle marker, including its border. */
function markerSize(radius: number): number {
  return 2 * radius + BORDER_WIDTH;
}

/** The polygon icon's corners in pixels from its center, at the given size. */
function polygonIconCorners(size: number): Coordinate[] {
  const center = POLYGON_ICON_VIEWBOX_SIZE / 2;
  const scale = size / POLYGON_ICON_VIEWBOX_SIZE;
  return POLYGON_ICON_CORNERS.map(([x, y]) => [(x - center) * scale, (y - center) * scale]);
}

/** How far the polygon marker for unclustered locations reaches from its center, at its farthest corner. */
export const UNCLUSTERED_POLYGON_MARKER_REACH = Math.max(
  ...polygonIconCorners(POLYGON_MARKER_SIZES.unclusteredLocation).map(([x, y]) => Math.hypot(x, y)),
);

/** Where a popped out polygon marker is drawn, and where its line to the cluster starts when it was moved there. */
export interface PopOut {
  at: Coordinate;
  lineFrom?: Coordinate;
}

/**
 * Where to draw the polygon markers popped out of the cluster drawn at clusterCenter, in the order of their locations.
 * A location clear of the cluster keeps its marker. The others move just clear of the cluster in the direction of their
 * location, fanned out so that they don't cover each other. Once any marker has to move, those close enough to cover
 * it or each other move along with it.
 */
export function popOuts(locations: Coordinate[], clusterCenter: Coordinate, resolution: number, rotation = 0): PopOut[] {
  const [centerX, centerY] = clusterCenter;
  const size = POLYGON_MARKER_SIZES.location;
  const corners = polygonIconCorners(size);
  // Markers are kept apart as circles around their farthest corner, rings included.
  const markerRadius = Math.max(...corners.map(([x, y]) => Math.hypot(x, y))) + SELECTED_RING_WIDTH;
  const clusterEdge = MARKERS.cluster.radius + BORDER_WIDTH / 2;
  const clearOfCluster = clusterEdge + POP_OUT_GAP + SELECTED_RING_WIDTH;
  // How far the icon, upright on screen whatever the map's rotation, reaches back towards the cluster.
  // Map y points up, screen y down.
  const reach = (angle: number) => {
    const screenAngle = angle - rotation;
    return Math.max(...corners.map(([x, y]) => -x * Math.cos(screenAngle) + y * Math.sin(screenAngle)));
  };
  const along = (angle: number, pixels: number): Coordinate => [
    centerX + Math.cos(angle) * pixels * resolution,
    centerY + Math.sin(angle) * pixels * resolution,
  ];

  // Straight up for a location at the cluster's center.
  const angles = locations.map(([x, y]) => (x === centerX && y === centerY ? Math.PI / 2 : Math.atan2(y - centerY, x - centerX)));
  // How much further out than a moved marker each location lies.
  const margins = locations.map(([x, y], i) => Math.hypot(x - centerX, y - centerY) / resolution - clearOfCluster - reach(angles[i]));
  const indexes = [...locations.keys()];
  const pixelsApart = (i: number, j: number) =>
    Math.hypot(locations[i][0] - locations[j][0], locations[i][1] - locations[j][1]) / resolution;
  // Too close to the cluster, or to another marker that keeps its location.
  const crowded = (i: number) => margins[i] < 0 || indexes.some((j) => j !== i && margins[j] >= 0 && pixelsApart(i, j) < 2 * markerRadius);
  const moved = indexes.some(crowded) ? indexes.filter((i) => margins[i] < 2 * markerRadius || crowded(i)) : [];
  // Adjacent circles touch at most, at the distance a marker moved straight across gets.
  const fanAngle = 2 * Math.asin(Math.min(1, markerRadius / (clearOfCluster + size / 2)));
  const fanned = fanOut(
    moved.map((i) => angles[i]),
    fanAngle,
  );
  return locations.map((location, i) => {
    const k = moved.indexOf(i);
    if (k < 0) return { at: location };
    const angle = fanned[k];
    return { at: along(angle, clearOfCluster + reach(angle)), lineFrom: along(angle, clusterEdge) };
  });
}

/** Spreads directions in radians at least minAngle apart, keeping their order around the circle. */
function fanOut(angles: number[], minAngle: number): number[] {
  const count = angles.length;
  if (count < 2) return angles;
  const order = [...angles.keys()].sort((a, b) => angles[a] - angles[b]);
  const sorted = order.map((i) => angles[i]);
  // Start after the widest gap, so that the directions only increase.
  const gaps = sorted.map((angle, k) => (k ? angle - sorted[k - 1] : angle + 2 * Math.PI - sorted[count - 1]));
  const start = gaps.indexOf(Math.max(...gaps));
  const unwrapped = sorted.map((_, k) => sorted[(start + k) % count] + (start + k < count ? 0 : 2 * Math.PI));
  let spread = spreadApart(unwrapped, minAngle);
  // Evenly around the circle when they don't fit.
  if (spread[count - 1] - spread[0] > 2 * Math.PI - minAngle) spread = unwrapped.map((_, k) => unwrapped[0] + (k * 2 * Math.PI) / count);
  const result: number[] = [];
  spread.forEach((angle, k) => (result[order[(start + k) % count]] = angle));
  return result;
}

interface AngleGroup {
  sum: number;
  count: number;
}

/** Spreads increasing angles at least minAngle apart, centering each group that has to spread on its angles' mean. */
function spreadApart(angles: number[], minAngle: number): number[] {
  const first = ({ sum, count }: AngleGroup) => sum / count - ((count - 1) / 2) * minAngle;
  const last = ({ sum, count }: AngleGroup) => sum / count + ((count - 1) / 2) * minAngle;
  const groups: AngleGroup[] = [];
  for (const angle of angles) {
    let group = { sum: angle, count: 1 };
    for (let previous = groups.at(-1); previous && first(group) - last(previous) < minAngle; previous = groups.at(-1)) {
      groups.pop();
      group = { sum: previous.sum + group.sum, count: previous.count + group.count };
    }
    groups.push(group);
  }
  return groups.flatMap((group) => Array.from({ length: group.count }, (_, k) => first(group) + k * minAngle));
}

export class MapFeatureStyles {
  private readonly palette: MapPalette;
  private readonly markers = new Map<string, Style[]>();
  private basePolygon?: Style[];
  private readonly polygonHighlights = new Map<MapFeatureState, Style[]>();

  constructor(styles: CSSStyleDeclaration) {
    this.palette = {
      primary: readColor(styles, COLOR_TOKENS.primary),
      hover: readColor(styles, COLOR_TOKENS.hover),
      border: readColor(styles, COLOR_TOKENS.border),
      hoverRing: readColor(styles, COLOR_TOKENS.hoverRing),
      selectedRing: readColor(styles, COLOR_TOKENS.selectedRing),
      negativeBorder: readColor(styles, COLOR_TOKENS.negativeBorder),
      negativeGlyph: readColor(styles, COLOR_TOKENS.negativeGlyph),
    };
  }

  marker(kind: MapMarkerKind, state: MapFeatureState, label = '', status: RegistrationStatus = 'found'): Style[] {
    const key = `${kind}/${state}/${label}/${status}`;
    const cached = this.markers.get(key);
    if (cached) return cached;

    const { radius, font } = MARKERS[kind];
    const styles = this.markerRing(state, (color, width) => circleRingRenderer(radius, color, width));
    styles.push(
      new Style({
        zIndex: 1,
        image:
          status === 'found'
            ? new CircleStyle({
                radius,
                fill: new Fill({ color: this.fill(state) }),
                stroke: new Stroke({ color: this.palette.border, width: BORDER_WIDTH }),
              })
            : new Icon({ src: this.negativeMarkerSrc(radius, status), scale: 1 / ICON_RASTER_SCALE }),
        text: font ? new Text({ text: label, font, fill: new Fill({ color: 'white' }) }) : undefined,
      }),
    );
    this.markers.set(key, styles);
    return styles;
  }

  /** The marker for the point of a location drawn as a polygon, with the count inside when a label is given. */
  polygonMarker(kind: PolygonMarkerKind, state: MapFeatureState, label = ''): Style[] {
    const key = `polygon/${kind}/${state}/${label}`;
    const cached = this.markers.get(key);
    if (cached) return cached;

    const styles = this.polygonMarkerStyles(kind, state, label);
    this.markers.set(key, styles);
    return styles;
  }

  poppedOutPolygonMarker(state: MapFeatureState, label: string, { at, lineFrom }: PopOut): Style[] {
    const marker = this.polygonMarkerStyles('location', state, label, new Point(at));
    if (!lineFrom) return marker;
    const line = new Style({
      zIndex: 0,
      // Ends under the icon, whichever side or corner of it faces the cluster.
      geometry: new LineString([lineFrom, at]),
      renderer: lineRenderer(this.palette.primary),
      // A click next to the cluster must reach the cluster.
      hitDetectionRenderer: () => undefined,
    });
    return [line, ...marker];
  }

  /**
   * The default location markers for WebGL layers, which draw hover and selected states on canvas overlays.
   * Not found locations are drawn last, on top of found ones.
   */
  webglLocationMarker(): Rule[] {
    const circle: FlatStyle = {
      'circle-radius': UNCLUSTERED_LOCATION_RADIUS,
      'circle-fill-color': this.palette.primary,
      'circle-stroke-color': this.palette.border,
      'circle-stroke-width': BORDER_WIDTH,
    };
    const polygonSize = POLYGON_MARKER_SIZES.unclusteredLocation;
    const polygon: FlatStyle = {
      'icon-src': this.polygonIconSrc(polygonSize, this.palette.primary),
      'icon-width': polygonSize,
      'icon-height': polygonSize,
    };
    const found = ['==', ['get', 'registrationStatus'], 'found'];
    const size = markerSize(UNCLUSTERED_LOCATION_RADIUS);
    const negative = (status: Exclude<RegistrationStatus, 'found'>): Rule => ({
      filter: ['==', ['get', 'registrationStatus'], status],
      style: { 'icon-src': this.negativeMarkerSrc(UNCLUSTERED_LOCATION_RADIUS, status), 'icon-width': size, 'icon-height': size },
    });
    return [
      { filter: ['all', found, ['!', ['get', 'isPolygon']]], style: circle },
      { filter: ['all', found, ['get', 'isPolygon']], style: polygon },
      negative('notRecovered'),
      negative('absent'),
    ];
  }

  polygon(): Style[] {
    const [r, g, b] = this.palette.primary;
    this.basePolygon ??= [
      new Style({
        fill: new Fill({ color: [r, g, b, 0.06] }),
        stroke: new Stroke({ color: this.palette.primary, width: BORDER_WIDTH }),
      }),
    ];
    return this.basePolygon;
  }

  /** Drawn over the base polygon, which supplies the fill. */
  polygonHighlight(state: MapFeatureState): Style[] {
    const cached = this.polygonHighlights.get(state);
    if (cached) return cached;

    const ring = this.ring(state);
    const styles: Style[] = [];
    if (ring) {
      styles.push(
        new Style({
          zIndex: 0,
          renderer: exteriorRingRenderer(ring.color, ring.width),
          hitDetectionRenderer: () => undefined,
        }),
      );
    }
    // A higher style z-index draws the border after the custom renderer,
    // covering the inner 0.75px of its outward stroke.
    styles.push(
      new Style({
        zIndex: 1,
        stroke: new Stroke({ color: state === 'selected' ? this.palette.border : this.palette.primary, width: BORDER_WIDTH }),
      }),
    );
    this.polygonHighlights.set(state, styles);
    return styles;
  }

  clearMarkerCache(): void {
    this.markers.clear();
  }

  private polygonMarkerStyles(kind: PolygonMarkerKind, state: MapFeatureState, label: string, geometry?: Point): Style[] {
    const size = POLYGON_MARKER_SIZES[kind];
    const corners = polygonIconCorners(size);
    const styles = this.markerRing(state, (color, width) => polygonIconRingRenderer(corners, color, width), geometry);
    styles.push(
      new Style({
        geometry,
        zIndex: 1,
        image: new Icon({ src: this.polygonIconSrc(size, this.fill(state)), scale: 1 / ICON_RASTER_SCALE }),
        text: label ? new Text({ text: label, font: LABEL_FONT, fill: new Fill({ color: 'white' }) }) : undefined,
      }),
    );
    return styles;
  }

  private markerRing(state: MapFeatureState, ringRenderer: (color: Color, width: number) => RenderFunction, geometry?: Point): Style[] {
    const ring = this.ring(state);
    if (!ring) return [];
    return [
      new Style({
        geometry,
        zIndex: 0,
        renderer: ringRenderer(ring.color, ring.width),
        // Decorative rings must not increase the click/hover target.
        hitDetectionRenderer: () => undefined,
      }),
    ];
  }

  /** The polygon icon, with a border on the inside of its outline, kept at the same width whatever the size. */
  private polygonIconSrc(size: number, fill: Color): string {
    // Half of the stroke falls outside the outline and is clipped away.
    const borderWidth = (2 * BORDER_WIDTH * POLYGON_ICON_VIEWBOX_SIZE) / size;
    const path = `M${POLYGON_ICON_CORNERS.map(([x, y]) => `${x} ${y}`).join('L')}Z`;
    const svg =
      `<svg xmlns="http://www.w3.org/2000/svg" width="${size * ICON_RASTER_SCALE}" height="${size * ICON_RASTER_SCALE}" viewBox="0 0 ${POLYGON_ICON_VIEWBOX_SIZE} ${POLYGON_ICON_VIEWBOX_SIZE}">` +
      `<clipPath id="outline"><path d="${path}"/></clipPath>` +
      `<path d="${path}" fill="${asString(fill)}" stroke="${asString(this.palette.border)}" stroke-width="${borderWidth}" clip-path="url(#outline)"/>` +
      `</svg>`;
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  }

  /** The summary box marker for not found records: a red-ringed circle with a minus or an X. */
  private negativeMarkerSrc(radius: number, status: Exclude<RegistrationStatus, 'found'>): string {
    const size = markerSize(radius);
    const center = size / 2;
    const arm = radius * 0.45;
    const [start, end] = [center - arm, center + arm];
    const glyph = status === 'absent' ? `M${start} ${start}L${end} ${end}M${start} ${end}L${end} ${start}` : `M${start} ${center}H${end}`;
    const svg =
      `<svg xmlns="http://www.w3.org/2000/svg" width="${size * ICON_RASTER_SCALE}" height="${size * ICON_RASTER_SCALE}" viewBox="0 0 ${size} ${size}">` +
      `<circle cx="${center}" cy="${center}" r="${radius}" fill="${asString(this.palette.border)}" stroke="${asString(this.palette.negativeBorder)}" stroke-width="${BORDER_WIDTH}"/>` +
      `<path d="${glyph}" stroke="${asString(this.palette.negativeGlyph)}" stroke-width="${BORDER_WIDTH}"/>` +
      `</svg>`;
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
  }

  private fill(state: MapFeatureState): Color {
    return state === 'hover' ? this.palette.hover : this.palette.primary;
  }

  private ring(state: MapFeatureState): { color: Color; width: number } | null {
    if (state === 'selected') return { color: this.palette.selectedRing, width: SELECTED_RING_WIDTH };
    if (state === 'hover') return { color: this.palette.hoverRing, width: 4 };
    return null;
  }
}
