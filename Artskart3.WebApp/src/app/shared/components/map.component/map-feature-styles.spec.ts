import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import Polygon from 'ol/geom/Polygon';
import MultiPolygon from 'ol/geom/MultiPolygon';
import type { State } from 'ol/render';
import { Circle as CircleStyle } from 'ol/style';
import { MapFeatureStyles, MapFeatureState, MapMarkerKind } from './map-feature-styles';

function tokenStyles(): CSSStyleDeclaration {
  const styles = document.createElement('div').style;
  styles.setProperty('--adb-surface-accent-primary', '#005A71');
  styles.setProperty('--adb-surface-accent-hover', '#004557');
  styles.setProperty('--adb-border-base-subtle', '#D2DDE0');
  styles.setProperty('--adb-border-base-strong', '#768083');
  styles.setProperty('--adb-border-brand-4', '#F8AE00');
  return styles;
}

function canvasState(geometry: Point | Polygon | MultiPolygon, pixelRatio = 1) {
  const context = {
    canvas: Object.assign(document.createElement('canvas'), { width: 500, height: 500 }),
    save: vi.fn(),
    restore: vi.fn(),
    beginPath: vi.fn(),
    rect: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    closePath: vi.fn(),
    clip: vi.fn(),
    arc: vi.fn(),
    stroke: vi.fn(),
    lineWidth: 0,
    lineJoin: 'round',
    lineCap: 'round',
    strokeStyle: '',
  } satisfies Partial<CanvasRenderingContext2D>;
  const renderContext: Partial<CanvasRenderingContext2D> = context;
  const state: State = {
    context: renderContext as CanvasRenderingContext2D,
    geometry,
    feature: new Feature(geometry),
    pixelRatio,
    resolution: 25,
    rotation: Math.PI / 4,
  };
  return { context, state };
}

describe('MapFeatureStyles', () => {
  let styles: MapFeatureStyles;

  beforeEach(() => {
    styles = new MapFeatureStyles(tokenStyles());
  });

  it.each([
    ['location', 8, undefined],
    ['cluster', 17, '600 10px "Chivo", sans-serif'],
    ['area', 20, '600 10px "Chivo", sans-serif'],
    ['observation', 11, undefined],
  ] as const)('preserves %s marker dimensions and labels', (kind, radius, font) => {
    for (const state of ['default', 'hover', 'selected'] as const) {
      const layers = styles.marker(kind, state, '12 k');
      const base = layers[layers.length - 1];
      const circle = base.getImage();
      expect(circle).toBeInstanceOf(CircleStyle);
      if (!(circle instanceof CircleStyle)) throw new Error('Expected circle marker');
      expect(circle.getRadius()).toBe(radius);
      expect(circle.getStroke()?.getWidth()).toBe(1.5);
      expect(circle.getStroke()?.getColor()).toEqual([210, 221, 224, 1]);
      expect(circle.getFill()?.getColor()).toEqual(state === 'hover' ? [0, 69, 87, 1] : [0, 90, 113, 1]);
      expect(base.getText()?.getFont()).toBe(font);
      if (font) {
        expect(base.getText()?.getText()).toBe('12 k');
        expect(base.getText()?.getFill()?.getColor()).toBe('white');
      }
    }
  });

  it.each([1, 2])('draws exterior marker rings at pixel ratio %s without enlarging hit targets', (pixelRatio) => {
    for (const kind of ['location', 'cluster', 'area', 'observation'] satisfies MapMarkerKind[]) {
      for (const [state, width, color] of [
        ['hover', 4, 'rgba(118,128,131,1)'],
        ['selected', 5, 'rgba(248,174,0,1)'],
      ] as const) {
        const [ring, base] = styles.marker(kind, state);
        const image = base.getImage();
        if (!(image instanceof CircleStyle)) throw new Error('Expected circle marker');
        const { context, state: renderState } = canvasState(new Point([50, 50]), pixelRatio);
        ring.getRenderer()!([50, 50], renderState);
        expect(context.arc).toHaveBeenCalledWith(50, 50, (image.getRadius() + 0.75 + width / 2) * pixelRatio, 0, Math.PI * 2);
        expect(context.lineWidth).toBe(width * pixelRatio);
        expect(context.strokeStyle).toBe(color);
        expect(context.save).toHaveBeenCalledOnce();
        expect(context.restore).toHaveBeenCalledOnce();
        expect(ring.getZIndex()).toBeLessThan(base.getZIndex()!);
        context.stroke.mockClear();
        ring.getHitDetectionRenderer()!([50, 50], renderState);
        expect(context.stroke).not.toHaveBeenCalled();
      }
    }
  });

  it.each(['default', 'hover', 'selected'] satisfies MapFeatureState[])(
    'keeps polygon fill at 6%% with a visible border in %s state',
    (state) => {
      const layers = styles.polygon(state);
      const base = layers[layers.length - 1];
      expect(base.getFill()?.getColor()).toEqual(state === 'hover' ? [0, 69, 87, 0.06] : [0, 90, 113, 0.06]);
      expect(base.getStroke()?.getColor()).toEqual(state === 'selected' ? [210, 221, 224, 1] : [0, 90, 113, 1]);
      expect(base.getStroke()?.getWidth()).toBe(1.5);
      expect(layers).toHaveLength(state === 'default' ? 1 : 2);
    },
  );

  it.each([1, 2])('clips polygon rings outside all parts and keeps holes at pixel ratio %s', (pixelRatio) => {
    const outer = [
      [10, 10],
      [100, 10],
      [100, 100],
      [10, 100],
      [10, 10],
    ];
    const hole = [
      [30, 30],
      [60, 30],
      [60, 60],
      [30, 60],
      [30, 30],
    ];
    const other = [
      [80, 80],
      [120, 80],
      [120, 120],
      [80, 120],
      [80, 80],
    ];
    for (const geometry of [new Polygon([outer, hole]), new MultiPolygon([[outer, hole], [other]])]) {
      for (const [state, width] of [
        ['hover', 4],
        ['selected', 5],
      ] as const) {
        const [ring, base] = styles.polygon(state);
        const { context, state: renderState } = canvasState(geometry, pixelRatio);
        ring.getRenderer()!(geometry.getCoordinates(), renderState);
        const parts = geometry instanceof MultiPolygon ? 2 : 1;
        expect(context.rect).toHaveBeenCalledTimes(parts);
        expect(context.rect).toHaveBeenCalledWith(0, 0, 500, 500);
        expect(context.clip).toHaveBeenCalledTimes(parts);
        expect(context.clip).toHaveBeenCalledWith('evenodd');
        expect(context.moveTo).toHaveBeenCalledWith(30, 30);
        expect(context.lineWidth).toBe((1.5 + width * 2) * pixelRatio);
        expect(context.lineJoin).toBe('round');
        expect(context.save).toHaveBeenCalledOnce();
        expect(context.restore).toHaveBeenCalledOnce();
        expect(ring.getZIndex()).toBeLessThan(base.getZIndex()!);
        context.stroke.mockClear();
        ring.getHitDetectionRenderer()!(geometry.getCoordinates(), renderState);
        expect(context.stroke).not.toHaveBeenCalled();
      }
    }
  });

  it('caches by state, marker variant, and label and supports language cache invalidation', () => {
    const marker = styles.marker('cluster', 'hover', '12 k');
    expect(styles.marker('cluster', 'hover', '12 k')).toBe(marker);
    expect(styles.marker('cluster', 'selected', '12 k')).not.toBe(marker);
    expect(styles.marker('area', 'hover', '12 k')).not.toBe(marker);
    expect(styles.marker('cluster', 'hover', '12 thousand')).not.toBe(marker);
    expect(styles.polygon('hover')).toBe(styles.polygon('hover'));
    styles.clearMarkerCache();
    expect(styles.marker('cluster', 'hover', '12 k')).not.toBe(marker);
  });

  it('reads the supplied computed tokens rather than hardcoded colors', () => {
    const tokens = tokenStyles();
    tokens.setProperty('--adb-surface-accent-primary', 'rgb(10, 20, 30)');
    const custom = new MapFeatureStyles(tokens);
    expect(custom.polygon('default')[0].getFill()?.getColor()).toEqual([10, 20, 30, 0.06]);
  });

  it.each(['', 'var(--unresolved)', 'not-a-color'])('rejects a missing or invalid token: %s', (value) => {
    const tokens = tokenStyles();
    tokens.setProperty('--adb-surface-accent-primary', value);
    expect(() => new MapFeatureStyles(tokens)).toThrow(/map color token: --adb-surface-accent-primary/);
  });
});
