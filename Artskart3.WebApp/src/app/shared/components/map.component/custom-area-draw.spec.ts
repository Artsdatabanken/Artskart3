import { MapEvents, NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import type { Mock } from 'vitest';
import { Feature } from 'ol';
import GeoJSON from 'ol/format/GeoJSON';
import { Polygon } from 'ol/geom';
import { CustomAreaDraw } from './custom-area-draw';

const PROJECTION = 'EPSG:25833';
const TRIANGLE = [
  [262000, 6650000],
  [263000, 6650000],
  [263000, 6651000],
  [262000, 6650000],
];
const TRIANGLE_WKT = 'POLYGON((262000 6650000,263000 6650000,263000 6651000,262000 6650000))';

function fakeMap() {
  const handlers = new Map<string, (payload: unknown) => void>();
  const map = {
    on: vi.fn((type: string, handler: (payload: unknown) => void) => {
      handlers.set(type, handler);
      return () => handlers.delete(type);
    }),
    startDrawing: vi.fn(),
    stopDrawing: vi.fn(),
    finishCurrent: vi.fn(),
    undoLastPoint: vi.fn(),
    abortCurrent: vi.fn(),
    clearDrawn: vi.fn(),
    importDrawnGeoJSON: vi.fn(),
    enableDrawEditing: vi.fn(),
    disableDrawEditing: vi.fn(),
  };
  const emit = (type: string, payload: unknown) => handlers.get(type)?.(payload);
  return { map, emit, handlers };
}

describe('CustomAreaDraw', () => {
  let map: ReturnType<typeof fakeMap>['map'];
  let emit: ReturnType<typeof fakeMap>['emit'];
  let handlers: ReturnType<typeof fakeMap>['handlers'];
  let callbacks: {
    areaChanged: Mock<(wkt: string) => void>;
    pointCountChanged: Mock<(count: number) => void>;
    drawingStopped: Mock<() => void>;
  };
  let draw: CustomAreaDraw;

  const polygonFeature = (ring: number[][]) => new Feature(new Polygon([ring]));
  const drawEnd = async (ring: number[][]) => {
    emit(MapEvents.DrawEnd, { feature: polygonFeature(ring) });
    await Promise.resolve();
  };
  const importedRing = () => {
    const [json] = map.importDrawnGeoJSON.mock.lastCall as [string];
    const [feature] = new GeoJSON().readFeatures(json, { featureProjection: PROJECTION });
    return (feature.getGeometry() as Polygon).getCoordinates()[0].map(([x, y]) => [Math.round(x), Math.round(y)]);
  };

  beforeEach(() => {
    ({ map, emit, handlers } = fakeMap());
    callbacks = { areaChanged: vi.fn(), pointCountChanged: vi.fn(), drawingStopped: vi.fn() };
    draw = new CustomAreaDraw(
      map as unknown as NbicMapComponent,
      () => PROJECTION,
      { normal: { strokeColor: '#0C936B' }, rejected: { strokeColor: '#B80738' } },
      callbacks,
    );
  });

  it('starter polygontegning uten redigering av det forrige', () => {
    draw.run('start');

    expect(map.disableDrawEditing).toHaveBeenCalled();
    expect(map.startDrawing).toHaveBeenCalledWith(expect.objectContaining({ kind: 'Polygon' }));
  });

  it('teller hjørnene som er satt', () => {
    emit(MapEvents.DrawStart, { kind: 'Polygon' });
    emit(MapEvents.DrawVertex, { kind: 'Polygon', index: 3, coordinate: [0, 0] });
    emit(MapEvents.DrawVertexRemoved, { kind: 'Polygon', index: 3 });

    expect(callbacks.pointCountChanged.mock.calls).toEqual([[1], [3], [2]]);
  });

  it('gir det ferdige polygonet som WKT i EPSG:25833 og gjør det redigerbart', async () => {
    draw.run('start');
    await drawEnd(TRIANGLE);

    expect(callbacks.areaChanged).toHaveBeenCalledWith(TRIANGLE_WKT);
    expect(callbacks.drawingStopped).toHaveBeenCalled();
    expect(map.stopDrawing).toHaveBeenCalled();
    expect(map.importDrawnGeoJSON).toHaveBeenCalledWith(expect.any(String), { clearExisting: true });
    expect(importedRing()).toEqual(TRIANGLE);
    expect(map.enableDrawEditing).toHaveBeenCalled();
  });

  it('oppgir projeksjonen i GeoJSON-en, siden importen ikke projiserer om', () => {
    draw.show(TRIANGLE_WKT);

    const [json] = map.importDrawnGeoJSON.mock.lastCall as [string];
    expect(JSON.parse(json).crs).toEqual({ type: 'name', properties: { name: PROJECTION } });
  });

  it('forkaster et polygon med færre enn tre hjørner og fortsetter tegningen', async () => {
    draw.run('start');
    await drawEnd([TRIANGLE[0], TRIANGLE[1], TRIANGLE[0]]);

    expect(callbacks.areaChanged).not.toHaveBeenCalled();
    expect(map.stopDrawing).not.toHaveBeenCalled();
    expect(map.clearDrawn).toHaveBeenCalled();
  });

  it('gir det nye polygonet når hjørnene flyttes, uten å tegne det på nytt', () => {
    draw.show(TRIANGLE_WKT);
    map.importDrawnGeoJSON.mockClear();
    const moved = [[262000, 6650000], [264000, 6650000], [263000, 6651000], [262000, 6650000]];

    emit(MapEvents.EditModified, { count: 1, feature: polygonFeature(moved) });
    const [wkt] = callbacks.areaChanged.mock.lastCall as [string];
    draw.show(wkt);

    expect(wkt).toBe('POLYGON((262000 6650000,264000 6650000,263000 6651000,262000 6650000))');
    expect(map.importDrawnGeoJSON).not.toHaveBeenCalled();
  });

  it('tegner et avvist polygon i egen stil, og bytter tilbake når det godtas', () => {
    const style = () => JSON.parse((map.importDrawnGeoJSON.mock.lastCall as [string])[0]).features[0].properties['nbic:style'];

    draw.show(TRIANGLE_WKT, true);
    expect(style()).toEqual({ strokeColor: '#B80738' });
    expect(map.enableDrawEditing).toHaveBeenCalled();

    draw.show(TRIANGLE_WKT, false);
    expect(style()).toEqual({ strokeColor: '#0C936B' });
  });

  it('fjerner polygonet når filteret fjernes', () => {
    draw.show(TRIANGLE_WKT);
    draw.show(null);

    expect(map.clearDrawn).toHaveBeenCalled();
    expect(map.disableDrawEditing).toHaveBeenCalled();
  });

  it('avbryter uten å endre filteret, og viser det forrige polygonet igjen', () => {
    draw.show(TRIANGLE_WKT);
    draw.run('start');
    map.importDrawnGeoJSON.mockClear();

    draw.run('cancel');

    expect(map.abortCurrent).toHaveBeenCalled();
    expect(map.stopDrawing).toHaveBeenCalled();
    expect(callbacks.drawingStopped).toHaveBeenCalled();
    expect(callbacks.areaChanged).not.toHaveBeenCalled();
    expect(importedRing()).toEqual(TRIANGLE);
  });

  it('slutter å lytte når den kastes', () => {
    draw.dispose();

    expect(handlers.size).toBe(0);
  });

  it('nullstiller hjørnene når Escape avbryter skissen', () => {
    draw.run('start');
    emit(MapEvents.DrawVertex, { index: 3 });
    callbacks.pointCountChanged.mockClear();

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(callbacks.pointCountChanged).toHaveBeenCalledWith(0);
  });

  it('lytter ikke på Escape når det ikke tegnes', () => {
    draw.run('start');
    draw.run('cancel');
    callbacks.pointCountChanged.mockClear();

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(callbacks.pointCountChanged).not.toHaveBeenCalled();
  });
});
