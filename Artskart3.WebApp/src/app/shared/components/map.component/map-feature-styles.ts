import { asArray, asString, type Color } from 'ol/color';
import type { Coordinate } from 'ol/coordinate';
import { Circle as CircleStyle, Fill, Stroke, Style, Text } from 'ol/style';
import type { RenderFunction } from 'ol/style/Style';

export type MapFeatureState = 'default' | 'hover' | 'selected';
export type MapMarkerKind = 'location' | 'cluster' | 'area' | 'observation';

const BORDER_WIDTH = 1.5;
const MARKERS = {
  location: { radius: 8, font: undefined },
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
} as const;

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

export class MapFeatureStyles {
  private readonly palette: MapPalette;
  private readonly markers = new Map<string, Style[]>();
  private readonly polygons = new Map<MapFeatureState, Style[]>();

  constructor(styles: CSSStyleDeclaration) {
    this.palette = {
      primary: readColor(styles, COLOR_TOKENS.primary),
      hover: readColor(styles, COLOR_TOKENS.hover),
      border: readColor(styles, COLOR_TOKENS.border),
      hoverRing: readColor(styles, COLOR_TOKENS.hoverRing),
      selectedRing: readColor(styles, COLOR_TOKENS.selectedRing),
    };
  }

  marker(kind: MapMarkerKind, state: MapFeatureState, label = ''): Style[] {
    const key = `${kind}/${state}/${label}`;
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
        image: new CircleStyle({
          radius,
          fill: new Fill({ color: this.fill(state) }),
          stroke: new Stroke({ color: this.palette.border, width: BORDER_WIDTH }),
        }),
        text: font ? new Text({ text: label, font, fill: new Fill({ color: 'white' }) }) : undefined,
      }),
    );
    this.markers.set(key, styles);
    return styles;
  }

  polygon(state: MapFeatureState): Style[] {
    const cached = this.polygons.get(state);
    if (cached) return cached;

    const ring = this.ring(state);
    const fill = this.fill(state);
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
    // A higher style z-index draws the base border after the custom renderer,
    // covering the inner 0.75px of its outward stroke without tinting the fill.
    styles.push(
      new Style({
        zIndex: 1,
        fill: new Fill({ color: [fill[0], fill[1], fill[2], 0.06] }),
        stroke: new Stroke({ color: state === 'selected' ? this.palette.border : this.palette.primary, width: BORDER_WIDTH }),
      }),
    );
    this.polygons.set(state, styles);
    return styles;
  }

  clearMarkerCache(): void {
    this.markers.clear();
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
