import { parseFilterLink } from '../utils/filter-link';
import { translateArtskart2Filter, translateArtskart2View } from './artskart2-filter';

const YEAR = 2026;
const translate = (source: Record<string, unknown>) => translateArtskart2Filter(source, YEAR);

/** Artskart 2's defaults: present, recovered and not hidden by annotation. */
const DEFAULTS = { Found: [2], NotRecovered: [2], Blocked: [2] };

describe('translateArtskart2Filter', () => {
  it('translates a taxon-page link to taxon and found-only, with nothing dropped', () => {
    const result = translate({
      TaxonIds: [26016],
      IncludeSubTaxonIds: true,
      ...DEFAULTS,
      Style: 1,
      YearFrom: 1700,
      YearTo: YEAR + 1,
      CoordinatePrecisionTo: 10000,
    });

    expect(result).toEqual({ filter: { taxonIds: [26016], registrationStatusId: 1 }, dropped: [], areaIds: [], catalogNumbers: [], collectionIds: [], nodeIds: [] });
  });

  it('translates every filter that maps directly', () => {
    const { filter, dropped } = translate({
      TaxonIds: ['1'],
      IncludeSubTaxonIds: false,
      TaxonGroupIds: [2],
      Categories: [3],
      BasisOfRecords: [0, 14],
      Behaviors: [6],
      InstitutionIds: [7],
      Months: [5, 6],
      YearFrom: 1990,
      YearTo: YEAR,
      CoordinatePrecisionFrom: 10,
      CoordinatePrecisionTo: 1000,
      Img: [1],
      UnsureId: [2],
      Spontan: [1],
      Sensitive: [2],
      Geometry: 'POLYGON((250000 6640000,270000 6640000,270000 6660000,250000 6640000))',
      ...DEFAULTS,
    });

    expect(dropped).toEqual([]);
    expect(filter).toEqual({
      taxonIds: [1],
      includeDescendantTaxa: false,
      taxonGroupIds: [2],
      categoryIds: [3],
      basisOfRecordIds: [0, 14],
      behaviorIds: [6],
      organizationIds: [7],
      period: { from: 1990, to: YEAR, months: [5, 6] },
      coordinatePrecision: { from: 10, to: 1000 },
      withImages: true,
      uncertainIdentification: false,
      introduced: true,
      sensitive: false,
      registrationStatusId: 1,
      customAreaWkt: 'POLYGON((250000 6640000,270000 6640000,270000 6660000,250000 6640000))',
    });
    expect(parseFilterLink(JSON.stringify(filter))?.dropped).toEqual([]);
  });

  it.each([
    [{ Found: [2], NotRecovered: [2] }, 1],
    [{ Found: [1] }, 2],
    [{ NotRecovered: [1] }, 3],
    [{ Found: [1], NotRecovered: [2] }, 2],
  ])('maps %j to registration status %i', (source, status) => {
    expect(translate({ ...source, Blocked: [2] })).toMatchObject({ filter: { registrationStatusId: status }, dropped: [] });
  });

  it('leaves out the registration status when all are included', () => {
    expect(translate({ Found: [1, 2], Blocked: [2] })).toMatchObject({ filter: {}, dropped: [] });
  });

  it('translates "show found" without NotRecovered as found only', () => {
    expect(translate({ Found: [2], Blocked: [2] })).toMatchObject({ filter: { registrationStatusId: 1 }, dropped: [] });
  });

  it('drops a registration status combination Artskart 3 has no single choice for', () => {
    const { filter, dropped } = translate({ NotRecovered: [2], Blocked: [2] });

    expect(filter.registrationStatusId).toBeUndefined();
    expect(dropped).toEqual(['NotRecovered']);
  });

  it.each([
    [{ Blocked: [2] }, {}, []],
    [{ Blocked: [2], Annotated: [1] }, { changedByAnnotation: true }, []],
    [{ Blocked: [1] }, { hiddenObservations: 'Only' }, []],
    [{}, { hiddenObservations: 'Include' }, []],
    [{ Blocked: [2], Annotated: [2] }, {}, ['Annotated']],
    [{ Annotated: [1] }, { hiddenObservations: 'Include' }, ['Annotated']],
  ])('maps the annotation filters %j', (source, filter, dropped) => {
    expect(translate(source)).toMatchObject({ filter, dropped });
  });

  it('drops filters with no Artskart 3 equivalent and names them', () => {
    const { filter, dropped } = translate({ ...DEFAULTS, Errors: [2], Valid: [1], LocationId: 12, AfterTrackDateTime: '2020-01-01', Unknown: 'x' });

    expect(filter).toEqual({ registrationStatusId: 1 });
    expect(dropped).toEqual(['Errors', 'Valid', 'LocationId', 'AfterTrackDateTime', 'Unknown']);
  });

  it('ignores display settings, empty values and filters that never had an effect', () => {
    const result = translate({
      ...DEFAULTS,
      Errors: [],
      Valid: [1, 2],
      Cons: [1],
      CenterPoints: true,
      BoundingBox: 'POLYGON((0 0,1 0,1 1,0 0))',
      Matr: 'bmFtZQ==',
      LocationId: 0,
      CollectionIds: [],
      AfterTrackDateTime: '',
    });

    expect(result).toEqual({ filter: { registrationStatusId: 1 }, dropped: [], areaIds: [], catalogNumbers: [], collectionIds: [], nodeIds: [] });
  });

  it('drops values of the wrong shape', () => {
    const { filter, dropped } = translate({ ...DEFAULTS, TaxonIds: 26016, Categories: ['x'], Months: [13], Img: [3], YearFrom: 'soon' });

    expect(filter).toEqual({ registrationStatusId: 1 });
    expect(dropped).toEqual(['TaxonIds', 'Categories', 'Months', 'YearFrom', 'Img']);
  });

  it('returns what needs a lookup separately', () => {
    expect(translate({ ...DEFAULTS, AreaIds: [1, '2'], CatalogNumbers: ['O-123', 456], CollectionIds: [441], NodeIds: [34] })).toMatchObject({
      areaIds: [1, 2],
      catalogNumbers: ['O-123', '456'],
      collectionIds: [441],
      nodeIds: [34],
      dropped: [],
    });
  });

  it('drops import sources given as "not from"', () => {
    expect(translate({ ...DEFAULTS, NodeIds: [-34] })).toMatchObject({ nodeIds: [], dropped: ['NodeIds'] });
  });

  it('ignores what a stored bookmark keeps besides the filter', () => {
    const bookmark = {
      Id: '45652bc7-5911-4918-ae64-0007e7969f73',
      Name: 'The Great Filter',
      LocationId: 0,
      TaxonIds: [59379, 1131],
      IncludeSubTaxonIds: true,
      Tags: [-5],
      EpsgCode: 32633,
      GroupBy: 0,
      Style: 1,
      IndexFrom: 0,
      IndexTo: 25,
      IncludeEmptyCoordinates: false,
      AfterTrackDateTime: null,
    };

    expect(translate(bookmark)).toMatchObject({ filter: { taxonIds: [59379, 1131], hiddenObservations: 'Include' }, dropped: [] });
  });
});

describe('translateArtskart2View', () => {
  it('turns the centre and zoom into the area a 1280 x 800 map showed in Artskart 2', () => {
    // Zoom 10 is 21.15625 m per pixel: 640 px either side is 13,540 m, 400 px is 8,462.5 m.
    expect(translateArtskart2View('262000,6650000', '10')).toEqual([248460, 6641538, 275540, 6658463]);
  });

  it('halves the area for each zoom level, also between levels', () => {
    const [minX, , maxX] = translateArtskart2View('262000,6650000', '11')!;
    expect(maxX - minX).toBe(13540);
    const [halfMinX, , halfMaxX] = translateArtskart2View('262000,6650000', '10.5')!;
    expect(halfMaxX - halfMinX).toBeCloseTo(27080 / Math.SQRT2, -1);
  });

  it("keeps Artskart 3's own default view for links that never moved the map", () => {
    expect(translateArtskart2View('427864,7623020', '3')).toBeNull();
    expect(translateArtskart2View('427864,7623020', '4')).not.toBeNull();
  });

  it.each([
    ['1,2', 'x'],
    ['1,2', ''],
    ['1,2', '19'],
    ['1,2', '-1'],
    ['a,b', '5'],
    ['1', '5'],
    ['1,2,3', '5'],
    [',2', '5'],
  ])('ignores centre %j with zoom %j', (center, zoom) => {
    expect(translateArtskart2View(center, zoom)).toBeNull();
  });
});
