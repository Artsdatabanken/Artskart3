import { AreaTypeId } from '@shared/models/area/area-marker.model';
import { AreaSearchResultDto } from '../../types/api.types';

type Translate = (key: string, params?: Record<string, unknown>) => string;

/** An area search result with the text shown after its name in the suggestions. */
export interface AreaSuggestion {
  area: AreaSearchResultDto;
  detail: string;
}

function historicalKey(area: AreaSearchResultDto): string {
  switch (area.areaTypeId) {
    case AreaTypeId.Municipality:
      return area.countyName ? 'sidebar.areaSearchType.historicalMunicipality' : 'sidebar.areaSearchType.historicalMunicipalityWithoutCounty';
    case AreaTypeId.County:
      return 'sidebar.areaSearchType.historicalCounty';
    default:
      return 'sidebar.areaSearchType.historicalArea';
  }
}

/**
 * What tells a historical area apart from the current one and from earlier versions with the
 * same name, e.g. "(historisk kommune i Troms, før 2020)". Empty for current areas.
 */
export function areaType(area: AreaSearchResultDto, translate: Translate): string {
  if (area.isCurrent) return '';
  return `(${translate(historicalKey(area), { county: area.countyName, year: area.endYear })})`;
}

export function areaLabel(area: AreaSearchResultDto, translate: Translate): string {
  return [area.name, areaType(area, translate)].filter(Boolean).join(' ');
}

/**
 * Adds the text after each name in the area suggestions. Historical areas always get theirs.
 * Current municipalities and counties always say what they are, a municipality with its county,
 * e.g. "kommune (Trøndelag)". An ocean area only says so when another current suggestion has the
 * same name.
 */
export function withSuggestionDetails(areas: AreaSearchResultDto[], translate: Translate): AreaSuggestion[] {
  const currentNameCounts = new Map<string, number>();
  for (const area of areas.filter((a) => a.isCurrent)) {
    const name = area.name ?? '';
    currentNameCounts.set(name, (currentNameCounts.get(name) ?? 0) + 1);
  }

  return areas.map((area) => ({
    area,
    detail: area.isCurrent ? currentDetail(area, (currentNameCounts.get(area.name ?? '') ?? 0) > 1, translate) : areaType(area, translate),
  }));
}

function currentDetail(area: AreaSearchResultDto, hasNamesake: boolean, translate: Translate): string {
  switch (area.areaTypeId) {
    case AreaTypeId.Municipality: {
      const kind = translate('sidebar.areaSearchType.municipality');
      return area.countyName ? `${kind} (${area.countyName})` : kind;
    }
    case AreaTypeId.County:
      return translate('sidebar.areaSearchType.county');
    case AreaTypeId.OceanArea:
      return hasNamesake ? `(${translate('sidebar.areaSearchType.oceanArea')})` : '';
    default:
      return '';
  }
}
