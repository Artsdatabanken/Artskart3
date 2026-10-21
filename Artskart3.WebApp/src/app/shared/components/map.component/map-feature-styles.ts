import { asArray, asString, type Color } from 'ol/color';
import type { Coordinate } from 'ol/coordinate';
import type { FeatureLike } from 'ol/Feature';
import { Circle as CircleStyle, Fill, Icon, Stroke, Style, Text } from 'ol/style';
import type { FlatStyle, Rule } from 'ol/style/flat';
import type { RenderFunction } from 'ol/style/Style';
import type { RegistrationStatus } from '@shared/helpers/registration-status/registration-status';

export type MapFeatureState = 'default' | 'hover' | 'selected';
export type MapMarkerKind = 'location' | 'unclusteredLocation' | 'cluster' | 'area' | 'observation';

const BORDER_WIDTH = 1.5;
export const UNCLUSTERED_LOCATION_RADIUS = 6.4;
const MARKERS = {
  location: { radius: 8, font: undefined },
  unclusteredLocation: { radius: UNCLUSTERED_LOCATION_RADIUS, font: undefined },
  cluster: { radius: 17, font: '600 10px "Chivo", sans-serif' },
  area: { radius: 20, font: '600 10px "Chivo", sans-serif' },
  observation: { radius: 11, font: undefined },
} satisfies Record<MapMarkerKind, { radius: number; font: string | undefined }>;

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

function exteriorRingRenderer(color: Color, width: number): RenderFunction {
  return (coordinates, { context, geometry, pixelRatio }) => {
    // OL supplies pixel coordinates with the same nesting as the source geometry.
    const polygons = geometry.getType() === 'MultiPolygon' ? (coordinates as Coordinate[][][]) : [coordinates as Coordinate[][]];
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
    context.lineWidth = (BORDER_WIDTH + width * 2) * pixelRatio;
    context.lineJoin = 'round';
    context.lineCap = 'round';
    context.stroke();
    context.restore();
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

export function registrationStatusOf(feature: FeatureLike): RegistrationStatus {
  const status: unknown = feature.get('registrationStatus');
  return status === 'absent' || status === 'notRecovered' ? status : 'found';
}

/** Width and height of a circle marker, including its border. */
function markerSize(radius: number): number {
  return 2 * radius + BORDER_WIDTH;
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
    const ring = this.ring(state);
    const styles: Style[] = [];
    if (ring) {
      styles.push(
        new Style({
          zIndex: 0,
          renderer: circleRingRenderer(radius, ring.color, ring.width),
          // Decorative rings must not increase the click/hover target.
          hitDetectionRenderer: () => undefined,
        }),
      );
    }
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

  /**
   * The default location markers for WebGL layers, which draw hover and selected states on canvas overlays.
   * Not found locations are drawn last, on top of found ones.
   */
  webglLocationMarker(): Rule[] {
    const found: FlatStyle = {
      'circle-radius': UNCLUSTERED_LOCATION_RADIUS,
      'circle-fill-color': this.palette.primary,
      'circle-stroke-color': this.palette.border,
      'circle-stroke-width': BORDER_WIDTH,
    };
    const size = markerSize(UNCLUSTERED_LOCATION_RADIUS);
    const negative = (status: Exclude<RegistrationStatus, 'found'>): Rule => ({
      filter: ['==', ['get', 'registrationStatus'], status],
      style: { 'icon-src': this.negativeMarkerSrc(UNCLUSTERED_LOCATION_RADIUS, status), 'icon-width': size, 'icon-height': size },
    });
    return [{ filter: ['==', ['get', 'registrationStatus'], 'found'], style: found }, negative('notRecovered'), negative('absent')];
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
    if (state === 'selected') return { color: this.palette.selectedRing, width: 5 };
    if (state === 'hover') return { color: this.palette.hoverRing, width: 4 };
    return null;
  }
}
