import { ObservationSearchFilter } from '../types/api.types';
import { isSavedFilterId, parseExtent, parseFilterLink } from './filter-link';

describe('parseFilterLink', () => {
  /**
   * Together with the applyFilter round-trip test in SearchFilterService, this keeps every link
   * filter unchanged through the filter panel. The type fails to compile when the API gets a new field.
   */
  it('accepts every filter field unchanged', () => {
    const sample: Required<Omit<ObservationSearchFilter, 'pageNumber' | 'resultsPerPage' | 'isPaginated'>> = {
      taxonGroupIds: [1],
      taxonIds: [2],
      includeDescendantTaxa: false,
      categoryIds: [3],
      organizationIds: [4],
      municipalityIds: ['1101'],
      countyIds: ['21'],
      oceanAreaIds: ['h1'],
      restrictedAreaIds: ['Naturbase VV00001234'],
      cadastralUnitIds: ['1101-1/2'],
      historicalAreaIds: [669],
      customAreaWkt: 'POLYGON((0 0,10 0,10 10,0 0))',
      behaviorIds: [5],
      basisOfRecordIds: [0, 6],
      registrationStatusId: 1,
      coordinatePrecision: { from: 0, to: 100 },
      period: { from: 1990, to: null, months: [5, 6] },
      datasetOrgIds: [7, 70],
      projectOrgIds: [8],
      observationIds: [9],
      withImages: true,
      changedByAnnotation: true,
      hiddenObservations: 'Only',
      sensitive: false,
      uncertainIdentification: true,
      introduced: false,
    };

    expect(parseFilterLink(JSON.stringify(sample))).toEqual({ filter: sample, dropped: [] });
  });

  it('drops unknown fields and values of the wrong shape, and names them', () => {
    const link = parseFilterLink(
      JSON.stringify({
        taxonIds: [26016],
        Errors: [2],
        pageNumber: 3,
        categoryIds: ['3'],
        registrationStatusId: 1.5,
        period: { from: 1990, until: 2000 },
        hiddenObservations: 'Some',
        constructor: 1,
      }),
    );

    expect(link).toEqual({
      filter: { taxonIds: [26016] },
      dropped: ['Errors', 'pageNumber', 'categoryIds', 'registrationStatusId', 'period', 'hiddenObservations', 'constructor'],
    });
  });

  it('treats null as not set', () => {
    expect(parseFilterLink('{"taxonIds":null,"withImages":true}')).toEqual({ filter: { withImages: true }, dropped: [] });
  });

  it('rejects months outside 1 to 12', () => {
    expect(parseFilterLink('{"period":{"months":[0,12]}}')?.dropped).toEqual(['period']);
  });

  /** A closed polygon with `points` points, the last repeating the first. */
  const polygon = (points: number) => {
    const corners = Array.from({ length: points - 1 }, (_, i) => `${Math.round(1000 * Math.cos(i))} ${Math.round(1000 * Math.sin(i))}`);
    return `POLYGON((${[...corners, corners[0]].join(',')}))`;
  };
  const numbers = (count: number) => Array.from({ length: count }, (_, i) => i + 1);

  // Values the API's ObservationFilterValidator rejects, which would make every search fail.
  it.each([
    ['period', { from: 2020, to: 2010 }],
    ['period', { from: 0 }],
    ['period', { to: 10000 }],
    ['period', { months: [...numbers(12), 1] }],
    ['coordinatePrecision', { from: 1000, to: 100 }],
    ['registrationStatusId', 4],
    ['taxonIds', numbers(501)],
    ['municipalityIds', numbers(501).map(String)],
    ['observationIds', numbers(5001)],
    ['historicalAreaIds', numbers(21)],
    ['cadastralUnitIds', ['1101-1']],
    ['cadastralUnitIds', numbers(21).map((n) => `1101-1/${n}`)],
    ['customAreaWkt', 'MULTIPOLYGON(((0 0,10 0,10 10,0 0)))'],
    ['customAreaWkt', 'POLYGON((0 0,10 0,10 10,0 10))'],
    ['customAreaWkt', 'POLYGON((0 0,10 0,0 0))'],
    ['customAreaWkt', 'POLYGON((0 0,10 x,10 10,0 0))'],
    ['customAreaWkt', polygon(1001)],
  ])('drops %s %j, which the API would reject', (field, value) => {
    expect(parseFilterLink(JSON.stringify({ withImages: true, [field]: value }))).toEqual({ filter: { withImages: true }, dropped: [field] });
  });

  it('accepts values at the limits', () => {
    const filter = {
      period: { from: 2010, to: 2010, months: numbers(12) },
      coordinatePrecision: { from: 100, to: 100 },
      registrationStatusId: 3,
      taxonIds: numbers(500),
      observationIds: numbers(5000),
      historicalAreaIds: numbers(20),
      cadastralUnitIds: [' 1101 - 1/2 ', '1101-1/2/3/4'],
      customAreaWkt: polygon(1000),
    };

    expect(parseFilterLink(JSON.stringify(filter))).toEqual({ filter, dropped: [] });
  });

  it('accepts a polygon with a hole', () => {
    const customAreaWkt = 'POLYGON((0 0,10 0,10 10,0 10,0 0),(2 2,4 2,4 4,2 2))';

    expect(parseFilterLink(JSON.stringify({ customAreaWkt }))).toEqual({ filter: { customAreaWkt }, dropped: [] });
  });

  it.each(['not json', '[1,2]', '"text"', 'null', '42'])('returns null for %s', (json) => {
    expect(parseFilterLink(json)).toBeNull();
  });
});

describe('parseExtent', () => {
  it('reads four numbers, including negative East in western Norway', () => {
    expect(parseExtent('-60000,6640000,270000.5,6660000')).toEqual([-60000, 6640000, 270000.5, 6660000]);
  });

  it.each(['1,2,3', '1,2,3,4,5', '1,,3,4', 'a,2,3,4', '3,2,1,4', '1,4,3,2', '1,2,1,4', 'Infinity,2,3,4'])(
    'rejects %s',
    (value) => {
      expect(parseExtent(value)).toBeNull();
    },
  );
});

describe('isSavedFilterId', () => {
  it('recognises the GUID of a saved filter, which JSON never is', () => {
    expect(isSavedFilterId('0193f4d2-8c1a-7c3e-9b2a-4f5e6d7c8b9a')).toBe(true);
    expect(isSavedFilterId('{"taxonIds":[1]}')).toBe(false);
    expect(isSavedFilterId('42')).toBe(false);
  });
});
