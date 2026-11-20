import { AreaSearchResultDto } from '../../types/api.types';
import { areaLabel, areaType, withSuggestionDetails } from './area-label';

describe('area labels', () => {
  // Shows the parameters, so the tests can see which ones a text gets.
  const translate = (key: string, params?: Record<string, unknown>) =>
    params ? `${key}[${Object.values(params).filter((v) => v != null).join(',')}]` : key;

  const tromso1902: AreaSearchResultDto = { id: 2, fid: '1902', name: 'Tromsø', areaTypeId: 1, isCurrent: false, countyName: 'Troms', endYear: 2020 };

  it('gives a historical municipality its county and end year', () => {
    expect(areaType(tromso1902, translate)).toBe('(sidebar.areaSearchType.historicalMunicipality[Troms,2020])');
    expect(areaLabel(tromso1902, translate)).toBe('Tromsø (sidebar.areaSearchType.historicalMunicipality[Troms,2020])');
  });

  it('leaves the county out of a historical municipality without one', () => {
    const area = { ...tromso1902, countyName: null };

    expect(areaType(area, translate)).toBe('(sidebar.areaSearchType.historicalMunicipalityWithoutCounty[2020])');
  });

  it('gives a historical county its end year', () => {
    const area = { id: 672, fid: '15_2017', name: 'Møre og Romsdal', areaTypeId: 2, isCurrent: false, endYear: 2019 };

    expect(areaLabel(area, translate)).toBe('Møre og Romsdal (sidebar.areaSearchType.historicalCounty[2019])');
  });

  it('adds nothing to a current area', () => {
    const area = { id: 1, fid: '5501', name: 'Tromsø', areaTypeId: 1, isCurrent: true, countyName: 'Troms' };

    expect(areaLabel(area, translate)).toBe('Tromsø');
  });

  describe('suggestion details', () => {
    const current = (id: number, name: string, areaTypeId: number, countyName?: string): AreaSearchResultDto => ({
      id,
      fid: String(id),
      name,
      areaTypeId,
      isCurrent: true,
      countyName,
    });
    const details = (areas: AreaSearchResultDto[]) => withSuggestionDetails(areas, translate).map((a) => a.detail);

    it('gives a current municipality its kind and county', () => {
      expect(details([current(5006, 'Steinkjer', 1, 'Trøndelag')])).toEqual(['sidebar.areaSearchType.municipality (Trøndelag)']);
    });

    it('gives a current municipality without a county only its kind', () => {
      expect(details([current(5006, 'Steinkjer', 1)])).toEqual(['sidebar.areaSearchType.municipality']);
    });

    it('gives a current county its kind', () => {
      expect(details([current(50, 'Trøndelag', 2)])).toEqual(['sidebar.areaSearchType.county']);
    });

    it('tells current municipalities with the same name apart by county', () => {
      expect(details([current(1, 'Herøy', 1, 'Møre og Romsdal'), current(2, 'Herøy', 1, 'Nordland')])).toEqual([
        'sidebar.areaSearchType.municipality (Møre og Romsdal)',
        'sidebar.areaSearchType.municipality (Nordland)',
      ]);
    });

    it('tells a county and a municipality with the same name apart by kind', () => {
      expect(details([current(3, 'Oslo', 2), current(301, 'Oslo', 1, 'Oslo')])).toEqual([
        'sidebar.areaSearchType.county',
        'sidebar.areaSearchType.municipality (Oslo)',
      ]);
    });

    it('leaves an ocean area with a unique name without a detail', () => {
      expect(details([current(10, 'Skagerrak', 4)])).toEqual(['']);
    });

    it('gives an ocean area its kind when a current area has the same name', () => {
      expect(details([current(10, 'Vega', 4), current(1815, 'Vega', 1, 'Nordland')])).toEqual([
        '(sidebar.areaSearchType.oceanArea)',
        'sidebar.areaSearchType.municipality (Nordland)',
      ]);
    });

    it('does not count a historical area with the same name as a namesake', () => {
      expect(details([current(10, 'Tromsø', 4), tromso1902])).toEqual(['', '(sidebar.areaSearchType.historicalMunicipality[Troms,2020])']);
    });
  });
});
