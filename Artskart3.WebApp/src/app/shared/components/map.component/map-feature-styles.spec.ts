import Feature from 'ol/Feature';
import LineString from 'ol/geom/LineString';
import Point from 'ol/geom/Point';
import Polygon from 'ol/geom/Polygon';
import MultiPolygon from 'ol/geom/MultiPolygon';
import type { State } from 'ol/render';
import { Circle as CircleStyle, Icon } from 'ol/style';
import {
  hasPolygonMarker,
  MapFeatureStyles,
  MapFeatureState,
  MapMarkerKind,
  popOuts,
  registrationStatusOf,
  type PopOut,
} from './map-feature-styles';

function tokenStyles(): CSSStyleDeclaration {
  const styles = document.createElement('div').style;
  styles.setProperty('--adb-color-brand-freshwater-blue-90', '#005A71');
  styles.setProperty('--adb-color-brand-freshwater-blue-100', '#004557');
  styles.setProperty('--adb-color-neutrals-ocean-10', '#D2DDE0');
  styles.setProperty('--adb-color-neutrals-ocean-50', '#768083');
  styles.setProperty('--adb-color-brand-sun-70', '#F8AE00');
  styles.setProperty('--adb-color-special-100', '#F34F39');
  styles.setProperty('--adb-color-special-110', '#D61900');
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
    ['unclusteredLocation', 6.4, undefined],
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
    for (const kind of ['location', 'unclusteredLocation', 'cluster', 'area', 'observation'] satisfies MapMarkerKind[]) {
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

  function decodedSvg(src: string | undefined): string {
    const prefix = 'data:image/svg+xml;charset=utf-8,';
    expect(src?.startsWith(prefix)).toBe(true);
    return decodeURIComponent(src!.slice(prefix.length));
  }

  it.each([
    ['notRecovered', /<path d="M\S+ \S+H\S+"/],
    ['absent', /<path d="M\S+ \S+L\S+ \S+M\S+ \S+L\S+ \S+"/],
  ] as const)('draws %s location markers like the summary box, at the same size', (status, glyph) => {
    for (const kind of ['location', 'unclusteredLocation', 'observation'] satisfies MapMarkerKind[]) {
      for (const state of ['default', 'hover', 'selected'] as const) {
        const layers = styles.marker(kind, state, '', status);
        const icon = layers[layers.length - 1].getImage();
        if (!(icon instanceof Icon)) throw new Error('Expected icon marker');
        const found = styles.marker(kind, state).at(-1)!.getImage() as CircleStyle;
        const size = 2 * found.getRadius() + 1.5;
        const svg = decodedSvg(icon.getSrc());
        expect(svg).toContain(`width="${size * 4}" height="${size * 4}" viewBox="0 0 ${size} ${size}"`);
        expect(icon.getScale()).toBe(0.25);
        expect(svg).toContain(`r="${found.getRadius()}" fill="rgba(210,221,224,1)" stroke="rgba(243,79,57,1)" stroke-width="1.5"`);
        expect(svg).toMatch(glyph);
        expect(svg).toContain('stroke="rgba(214,25,0,1)"');
        expect(layers).toHaveLength(state === 'default' ? 1 : 2);
      }
    }
  });

  const POLYGON_ICON_PATH = 'M0 5L0 34.5L46 46L41 0Z';

  it.each([
    ['location', 46],
    ['unclusteredLocation', 20],
  ] as const)('draws %s polygon markers as the polygon icon with a border inside its outline and the label inside', (kind, size) => {
    for (const [state, fill] of [
      ['default', 'rgba(0,90,113,1)'],
      ['hover', 'rgba(0,69,87,1)'],
      ['selected', 'rgba(0,90,113,1)'],
    ] as const) {
      const layers = styles.polygonMarker(kind, state, '12 k');
      const base = layers.at(-1)!;
      const icon = base.getImage();
      if (!(icon instanceof Icon)) throw new Error('Expected icon marker');
      const svg = decodedSvg(icon.getSrc());
      expect(svg).toContain(`width="${size * 4}" height="${size * 4}" viewBox="0 0 46 46"`);
      expect(icon.getScale()).toBe(0.25);
      expect(svg).toContain(`<clipPath id="outline"><path d="${POLYGON_ICON_PATH}"/></clipPath>`);
      expect(svg).toContain(
        `<path d="${POLYGON_ICON_PATH}" fill="${fill}" stroke="rgba(210,221,224,1)" stroke-width="${(2 * 1.5 * 46) / size}" clip-path="url(#outline)"/>`,
      );
      expect(base.getText()?.getText()).toBe('12 k');
      expect(base.getText()?.getFont()).toBe('600 10px "Chivo", sans-serif');
      expect(base.getText()?.getFill()?.getColor()).toBe('white');
      expect(layers).toHaveLength(state === 'default' ? 1 : 2);
      expect(styles.polygonMarker(kind, state).at(-1)!.getText()).toBeNull();
    }
  });

  it.each([1, 2])('draws polygon marker rings along the icon outline at pixel ratio %s without enlarging hit targets', (pixelRatio) => {
    for (const [state, width, color] of [
      ['hover', 4, 'rgba(118,128,131,1)'],
      ['selected', 5, 'rgba(248,174,0,1)'],
    ] as const) {
      const [ring, base] = styles.polygonMarker('location', state);
      const { context, state: renderState } = canvasState(new Point([50, 50]), pixelRatio);
      ring.getRenderer()!([50, 50], renderState);
      expect(context.rect).toHaveBeenCalledWith(0, 0, 500, 500);
      expect(context.clip).toHaveBeenCalledWith('evenodd');
      // The corners, 23 px from the center to the edges of the 46 px icon.
      expect(context.moveTo).toHaveBeenCalledWith(50 - 23 * pixelRatio, 50 - 18 * pixelRatio);
      expect(context.lineTo).toHaveBeenCalledWith(50 - 23 * pixelRatio, 50 + 11.5 * pixelRatio);
      expect(context.lineTo).toHaveBeenCalledWith(50 + 23 * pixelRatio, 50 + 23 * pixelRatio);
      expect(context.lineTo).toHaveBeenCalledWith(50 + 18 * pixelRatio, 50 - 23 * pixelRatio);
      expect(context.lineWidth).toBe(width * 2 * pixelRatio);
      expect(context.lineJoin).toBe('round');
      expect(context.strokeStyle).toBe(color);
      expect(context.save).toHaveBeenCalledOnce();
      expect(context.restore).toHaveBeenCalledOnce();
      expect(ring.getZIndex()).toBeLessThan(base.getZIndex()!);
      context.stroke.mockClear();
      ring.getHitDetectionRenderer()!([50, 50], renderState);
      expect(context.stroke).not.toHaveBeenCalled();
    }
  });

  function expectDirection({ at, lineFrom }: PopOut, [x, y]: number[], angle: number): void {
    for (const [pointX, pointY] of [at, lineFrom!]) {
      const length = Math.hypot(pointX - x, pointY - y);
      expect((pointX - x) / length).toBeCloseTo(Math.cos(angle));
      expect((pointY - y) / length).toBeCloseTo(Math.sin(angle));
    }
  }

  function expectCloseTo([x, y]: number[], [expectedX, expectedY]: number[]): void {
    expect(x).toBeCloseTo(expectedX);
    expect(y).toBeCloseTo(expectedY);
  }

  // The icon reaches 23 px towards the cluster from straight across, and 34.5 / √2 px from the lower left corner.
  it.each([
    ['left of', [-3, 0], 23],
    ['below', [0, -3], 23],
    ['on', [0, 0], 23],
    ['up and right of', [3, 3], 34.5 * Math.SQRT1_2],
  ] as const)('moves a polygon marker located %s its cluster center just clear of the cluster', (_, [dx, dy], reach) => {
    const resolution = 2;
    const at = (pixelX: number, pixelY: number) => [1000 + pixelX * resolution, 2000 + pixelY * resolution];
    const [popOut, ...rest] = popOuts([at(dx, dy)], at(0, 0), resolution);
    expect(rest).toHaveLength(0);

    const length = Math.hypot(dx, dy);
    const [ux, uy] = length ? [dx / length, dy / length] : [0, 1];
    const distance = 17.75 + 10 + 5 + reach;
    expectCloseTo(popOut.at, at(ux * distance, uy * distance));
    expectCloseTo(popOut.lineFrom!, at(ux * 17.75, uy * 17.75));
  });

  it('leaves a polygon marker already clear of its cluster at its location', () => {
    expect(popOuts([[880, 2000]], [1000, 2000], 2)).toEqual([{ at: [880, 2000] }]);
  });

  // Circles around the icon's farthest corner and its ring touch at the 55.75 px a marker moved straight across gets.
  const markerRadius = Math.hypot(23, 23) + 5;
  const fanAngle = 2 * Math.asin(markerRadius / 55.75);

  it('fans out polygon markers moved in about the same direction, keeping their order around the cluster', () => {
    const center = [1000, 2000];
    const [above, below] = popOuts(
      [
        [994, 2000.1],
        [994, 1999.9],
      ],
      center,
      2,
    );
    expectDirection(above, center, Math.PI - fanAngle / 2);
    expectDirection(below, center, Math.PI + fanAngle / 2);
  });

  it('fans out only the polygon markers that would cover each other', () => {
    const center = [1000, 2000];
    const [right, left, alsoRight] = popOuts(
      [
        [1006, 2000],
        [994, 2000],
        [1006, 2000],
      ],
      center,
      2,
    );
    expectDirection(right, center, -fanAngle / 2);
    expectDirection(left, center, Math.PI);
    expectDirection(alsoRight, center, fanAngle / 2);
  });

  it('fans out a marker clear of the cluster along with a moved one it could cover, but leaves one further out', () => {
    const center = [1000, 2000];
    // 60 and 150 px left of the cluster, where a marker moved left lies 55.75 px out and is kept 75 px from others.
    const [near, moved, far] = popOuts(
      [
        [880, 2000],
        [994, 2000],
        [700, 2000],
      ],
      center,
      2,
    );
    expectDirection(near, center, Math.PI - fanAngle / 2);
    expectDirection(moved, center, Math.PI + fanAngle / 2);
    expect(far).toEqual({ at: [700, 2000] });
  });

  it('fans out markers that would keep their locations but cover each other', () => {
    const center = [1000, 2000];
    // 150 px left of the cluster and 5 px apart.
    const [first, second] = popOuts(
      [
        [700, 2000],
        [700, 2010],
      ],
      center,
      2,
    );
    const between = (Math.PI + Math.atan2(10, -300)) / 2;
    expectDirection(first, center, between + fanAngle / 2);
    expectDirection(second, center, between - fanAngle / 2);
  });

  it('keeps fanned out markers from covering each other all the way round the cluster', () => {
    // The icon's corners in pixels from its center, screen y down.
    const corners = [
      [-23, -18],
      [-23, 11.5],
      [23, 23],
      [18, -23],
    ];
    const outline = ([x, y]: number[]) => corners.map(([dx, dy]) => [x + dx, y - dy]);
    // Convex shapes are apart when one of their edges separates them.
    const apart = (a: number[][], b: number[][]) =>
      [a, b].some((shape) =>
        shape.some(([x1, y1], i) => {
          const [x2, y2] = shape[(i + 1) % shape.length];
          const across = (points: number[][]) => points.map(([x, y]) => (y2 - y1) * x - (x2 - x1) * y);
          const [onA, onB] = [across(a), across(b)];
          return Math.max(...onA) <= Math.min(...onB) || Math.max(...onB) <= Math.min(...onA);
        }),
      );
    for (let degrees = 0; degrees < 360; degrees += 5) {
      const angle = (degrees * Math.PI) / 180;
      const location = [3 * Math.cos(angle), 3 * Math.sin(angle)];
      const [first, second] = popOuts([location, location], [0, 0], 1);
      expect(apart(outline(first.at), outline(second.at))).toBe(true);
    }
  });

  it('measures how far the icon reaches towards the cluster as drawn on a rotated map', () => {
    // Up and right on the map is down and right on a map rotated a quarter turn, where the top left corner faces the cluster.
    const [popOut] = popOuts([[3, 3]], [0, 0], 1, Math.PI / 2);
    const distance = 17.75 + 10 + 5 + 41 * Math.SQRT1_2;
    expectCloseTo(popOut.at, [distance * Math.SQRT1_2, distance * Math.SQRT1_2]);
  });

  it('spreads polygon markers evenly around the cluster when they do not fit', () => {
    const center = [1000, 2000];
    const fanned = popOuts(
      Array.from({ length: 7 }, () => [994, 2000]),
      center,
      2,
    );
    fanned.forEach((popOut, i) => expectDirection(popOut, center, Math.PI + (i * 2 * Math.PI) / 7));
  });

  it('draws a popped out polygon marker with a line under it from the cluster, unless it kept its location', () => {
    const [line, ring, icon, ...rest] = styles.poppedOutPolygonMarker('selected', '80', { at: [10, 20], lineFrom: [30, 40] });
    expect(rest).toHaveLength(0);
    expect((line.getGeometry() as LineString).getCoordinates()).toEqual([
      [30, 40],
      [10, 20],
    ]);
    const { context, state: renderState } = canvasState(new Point([50, 50]), 2);
    line.getRenderer()!(
      [
        [10, 20],
        [30, 40],
      ],
      renderState,
    );
    expect(context.moveTo).toHaveBeenCalledWith(10, 20);
    expect(context.lineTo).toHaveBeenCalledWith(30, 40);
    expect(context.strokeStyle).toBe('rgba(0,90,113,1)');
    expect(context.lineWidth).toBe(3);
    context.stroke.mockClear();
    line.getHitDetectionRenderer()!([], renderState);
    expect(context.stroke).not.toHaveBeenCalled();
    expect(line.getZIndex()).toBeLessThan(icon.getZIndex()!);
    for (const style of [ring, icon]) expect((style.getGeometry() as Point).getCoordinates()).toEqual([10, 20]);
    expect(ring.getHitDetectionRenderer()!([50, 50], canvasState(new Point([50, 50])).state)).toBeUndefined();
    expect(icon.getText()?.getText()).toBe('80');

    const [keptRing, keptIcon, ...none] = styles.poppedOutPolygonMarker('selected', '80', { at: [10, 20] });
    expect(none).toHaveLength(0);
    for (const style of [keptRing, keptIcon]) expect((style.getGeometry() as Point).getCoordinates()).toEqual([10, 20]);
  });

  it('gives the polygon marker only to found locations drawn as polygons', () => {
    expect(hasPolygonMarker(new Feature({ isPolygon: true }))).toBe(true);
    expect(hasPolygonMarker(new Feature({ isPolygon: true, registrationStatus: 'notRecovered' }))).toBe(false);
    expect(hasPolygonMarker(new Feature({ isPolygon: false }))).toBe(false);
    expect(hasPolygonMarker(new Feature())).toBe(false);
  });

  it('reads the registration status of a feature and treats anything else as found', () => {
    expect(registrationStatusOf(new Feature({ registrationStatus: 'absent' }))).toBe('absent');
    expect(registrationStatusOf(new Feature({ registrationStatus: 'notRecovered' }))).toBe('notRecovered');
    expect(registrationStatusOf(new Feature({ registrationStatus: 'other' }))).toBe('found');
    expect(registrationStatusOf(new Feature())).toBe('found');
  });

  it('describes location markers as WebGL rules, drawing polygons over points and not found locations on top', () => {
    const [found, foundPolygon, notRecovered, absent, ...rest] = styles.webglLocationMarker();
    expect(rest).toHaveLength(0);
    const isFound = ['==', ['get', 'registrationStatus'], 'found'];
    expect(found).toEqual({
      filter: ['all', isFound, ['!', ['get', 'isPolygon']]],
      style: {
        'circle-radius': 6.4,
        'circle-fill-color': [0, 90, 113, 1],
        'circle-stroke-color': [210, 221, 224, 1],
        'circle-stroke-width': 1.5,
      },
    });
    const canvasPolygon = styles.polygonMarker('unclusteredLocation', 'default')[0].getImage() as Icon;
    expect(foundPolygon).toEqual({
      filter: ['all', isFound, ['get', 'isPolygon']],
      style: { 'icon-src': canvasPolygon.getSrc(), 'icon-width': 20, 'icon-height': 20 },
    });
    for (const [rule, status] of [
      [notRecovered, 'notRecovered'],
      [absent, 'absent'],
    ] as const) {
      const canvasIcon = styles.marker('unclusteredLocation', 'default', '', status)[0].getImage() as Icon;
      expect(rule).toEqual({
        filter: ['==', ['get', 'registrationStatus'], status],
        style: { 'icon-src': canvasIcon.getSrc(), 'icon-width': 14.3, 'icon-height': 14.3 },
      });
    }
  });

  it('keeps the base polygon fill at 6% with a visible border', () => {
    const [base, ...rest] = styles.polygon();
    expect(base.getFill()?.getColor()).toEqual([0, 90, 113, 0.06]);
    expect(base.getStroke()?.getColor()).toEqual([0, 90, 113, 1]);
    expect(base.getStroke()?.getWidth()).toBe(1.5);
    expect(rest).toHaveLength(0);
  });

  it.each(['default', 'hover', 'selected'] satisfies MapFeatureState[])(
    'leaves the fill to the base polygon and keeps a visible border in the %s highlight',
    (state) => {
      const layers = styles.polygonHighlight(state);
      const border = layers[layers.length - 1];
      expect(border.getFill()).toBeNull();
      expect(border.getStroke()?.getColor()).toEqual(state === 'selected' ? [210, 221, 224, 1] : [0, 90, 113, 1]);
      expect(border.getStroke()?.getWidth()).toBe(1.5);
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
        const [ring, base] = styles.polygonHighlight(state);
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
    expect(styles.marker('cluster', 'hover', '12 k', 'absent')).not.toBe(marker);
    expect(styles.polygonHighlight('hover')).toBe(styles.polygonHighlight('hover'));
    const polygonMarker = styles.polygonMarker('location', 'hover', '12 k');
    expect(styles.polygonMarker('location', 'hover', '12 k')).toBe(polygonMarker);
    expect(styles.polygonMarker('location', 'hover', '12 thousand')).not.toBe(polygonMarker);
    expect(styles.polygonMarker('unclusteredLocation', 'hover', '12 k')).not.toBe(polygonMarker);
    expect(styles.marker('location', 'hover', '12 k')).not.toBe(polygonMarker);
    styles.clearMarkerCache();
    expect(styles.marker('cluster', 'hover', '12 k')).not.toBe(marker);
    expect(styles.polygonMarker('location', 'hover', '12 k')).not.toBe(polygonMarker);
  });

  it('reads the supplied computed tokens rather than hardcoded colors', () => {
    const tokens = tokenStyles();
    tokens.setProperty('--adb-color-brand-freshwater-blue-90', 'rgb(10, 20, 30)');
    const custom = new MapFeatureStyles(tokens);
    expect(custom.polygon()[0].getFill()?.getColor()).toEqual([10, 20, 30, 0.06]);
  });

  it.each(['', 'var(--unresolved)', 'not-a-color'])('rejects a missing or invalid token: %s', (value) => {
    const tokens = tokenStyles();
    tokens.setProperty('--adb-color-brand-freshwater-blue-90', value);
    expect(() => new MapFeatureStyles(tokens)).toThrow(/map color token: --adb-color-brand-freshwater-blue-90/);
  });
});
