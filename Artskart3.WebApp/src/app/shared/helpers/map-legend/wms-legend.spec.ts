import { wmsLegendImages } from './wms-legend';

const wms = { id: 1, name: 'Vern', type: 'WMS', url: 'https://example.test/wms?', layers: 'a, b', format: 'image/png', version: '1.3.0' };

describe('wmsLegendImages', () => {
  it('lager én GetLegendGraphic-adresse per WMS-lag', () => {
    const images = wmsLegendImages(wms);

    expect(images.map((image) => image.layerName)).toEqual(['a', 'b']);
    const url = new URL(images[0].url);
    expect(url.origin + url.pathname).toBe('https://example.test/wms');
    expect(Object.fromEntries(url.searchParams)).toEqual({
      SERVICE: 'WMS',
      VERSION: '1.3.0',
      REQUEST: 'GetLegendGraphic',
      SLD_VERSION: '1.1.0',
      FORMAT: 'image/png',
      LAYER: 'a',
    });
  });

  it('beholder parametere som allerede står i adressen', () => {
    const [image] = wmsLegendImages({ ...wms, url: 'https://example.test/wms?map=vern', layers: 'a' });

    expect(new URL(image.url).searchParams.get('map')).toBe('vern');
  });

  it('gir ingen bilder for andre korttyper, manglende lagnavn eller ugyldig adresse', () => {
    expect(wmsLegendImages({ ...wms, type: 'WMTS' })).toEqual([]);
    expect(wmsLegendImages({ ...wms, layers: null })).toEqual([]);
    expect(wmsLegendImages({ ...wms, url: 'ikke en adresse' })).toEqual([]);
  });
});
