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

function kindKey(area: AreaSearchResultDto): string | null {
  switch (area.areaTypeId) {
    case AreaTypeId.Municipality:
      return 'sidebar.areaSearchType.municipality';
    case AreaTypeId.County:
      return 'sidebar.areaSearchType.county';
    case AreaTypeId.OceanArea:
      return 'sidebar.areaSearchType.oceanArea';
    default:
      return null;
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
 * A current area only gets one when another current suggestion has the same name: the county
 * when they are all municipalities (Herøy), otherwise the kind of area (Oslo is both).
 */
export function withSuggestionDetails(areas: AreaSearchResultDto[], translate: Translate): AreaSuggestion[] {
  const currentByName = new Map<string, AreaSearchResultDto[]>();
  for (const area of areas.filter((a) => a.isCurrent)) {
    const name = area.name ?? '';
    currentByName.set(name, [...(currentByName.get(name) ?? []), area]);
  }

  return areas.map((area) => ({
    area,
    detail: area.isCurrent ? currentDetail(area, currentByName.get(area.name ?? '') ?? [], translate) : areaType(area, translate),
  }));
}

function currentDetail(area: AreaSearchResultDto, sameName: AreaSearchResultDto[], translate: Translate): string {
  if (sameName.length < 2) return '';
  if (sameName.every((a) => a.areaTypeId === AreaTypeId.Municipality && a.countyName)) return `(${area.countyName})`;
  const key = kindKey(area);
  return key ? `(${translate(key)})` : '';
}
