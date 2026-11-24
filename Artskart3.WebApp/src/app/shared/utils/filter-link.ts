import { MapExtent } from '../services/map-view/map-view.service';
import { ObservationSearchFilter } from '../types/api.types';

export const FILTER_LINK_PARAM = 'filter';
export const EXTENT_LINK_PARAM = 'extent';
export const SAVED_FILTER_LINK_PARAM = 'savedFilter';

type LinkFilterField = keyof Omit<ObservationSearchFilter, 'pageNumber' | 'resultsPerPage' | 'isPaginated'>;

// The limits the API's ObservationFilterValidator enforces. A link past them would make every search fail.
const MAX_LIST = 500;
const MAX_OBSERVATION_IDS = 5000;
const MAX_HISTORICAL_AREAS = 20;
const MAX_CADASTRAL_UNITS = 20;
const MAX_CUSTOM_AREA_POINTS = 1000;
const MAX_CUSTOM_AREA_CHARACTERS = MAX_CUSTOM_AREA_POINTS * 64;
const MIN_YEAR = 1;
const MAX_YEAR = 9999;
const CADASTRAL_UNIT = /^[0-9]{4}-[0-9]{1,5}\/[0-9]{1,5}(?:\/[0-9]{1,5}){0,2}$/;
const SAVED_FILTER_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const isInteger = (value: unknown): value is number => Number.isInteger(value);
const isBoolean = (value: unknown): boolean => typeof value === 'boolean';
const isOptionalInteger = (value: unknown): boolean => value == null || isInteger(value);
const isYear = (value: unknown): boolean => value == null || (isInteger(value) && value >= MIN_YEAR && value <= MAX_YEAR);
const isMonth = (value: unknown): boolean => isInteger(value) && value >= 1 && value <= 12;
const isCadastralUnit = (value: unknown): boolean => typeof value === 'string' && CADASTRAL_UNIT.test(value.replace(/\s/g, ''));
const isList =
  (isItem: (item: unknown) => boolean, max = MAX_LIST) =>
  (value: unknown): boolean =>
    Array.isArray(value) && value.length <= max && value.every(isItem);
const isIntegerList = isList(isInteger);
const isStringList = isList((item) => typeof item === 'string');

function hasOnly(value: unknown, fields: Record<string, (value: unknown) => boolean>): value is Record<string, unknown> {
  return (
    typeof value === 'object' &&
    value !== null &&
    !Array.isArray(value) &&
    Object.entries(value).every(([key, item]) => Object.hasOwn(fields, key) && fields[key](item))
  );
}

/** A range whose ends are both set must not run backwards; the API only checks ends above zero for precision. */
function isOrdered(range: Record<string, unknown>, onlyPositive = false): boolean {
  const { from, to } = range as { from?: number | null; to?: number | null };
  if (from == null || to == null || (onlyPositive && (from <= 0 || to <= 0))) return true;
  return from <= to;
}

/** One closed POLYGON of at most 1,000 points, which is what the API's CustomArea.Parse accepts. */
function isPolygon(value: unknown): boolean {
  if (typeof value !== 'string' || value.length > MAX_CUSTOM_AREA_CHARACTERS) return false;
  const rings = /^\s*POLYGON\s*\(\s*(\(.*\))\s*\)\s*$/is.exec(value)?.[1].split(/\)\s*,\s*\(/);
  if (!rings) return false;

  let points = 0;
  for (const ring of rings) {
    const coordinates = ring
      .replace(/[()]/g, '')
      .split(',')
      .map((point) => point.trim().split(/\s+/).map(Number));
    if (coordinates.some((coordinate) => coordinate.length !== 2 || !coordinate.every(Number.isFinite))) return false;
    const first = coordinates[0];
    const last = coordinates[coordinates.length - 1];
    if (coordinates.length < 4 || first[0] !== last[0] || first[1] !== last[1]) return false;
    points += coordinates.length;
  }
  return points <= MAX_CUSTOM_AREA_POINTS;
}

/**
 * Every field a link may set, with the shape and limits its value must keep to. The type lists every
 * filter field, so a new field in the API doesn't compile until it is added here.
 */
const LINK_FILTER_FIELDS: Record<LinkFilterField, (value: unknown) => boolean> = {
  taxonGroupIds: isIntegerList,
  taxonIds: isIntegerList,
  includeDescendantTaxa: isBoolean,
  categoryIds: isIntegerList,
  organizationIds: isIntegerList,
  municipalityIds: isStringList,
  countyIds: isStringList,
  restrictedAreaIds: isStringList,
  oceanAreaIds: isStringList,
  cadastralUnitIds: isList(isCadastralUnit, MAX_CADASTRAL_UNITS),
  historicalAreaIds: isList(isInteger, MAX_HISTORICAL_AREAS),
  customAreaWkt: isPolygon,
  behaviorIds: isIntegerList,
  basisOfRecordIds: isIntegerList,
  registrationStatusId: (value) => isInteger(value) && value >= 1 && value <= 3,
  coordinatePrecision: (value) =>
    hasOnly(value, { from: isOptionalInteger, to: isOptionalInteger }) && isOrdered(value, true),
  period: (value) =>
    hasOnly(value, {
      from: isYear,
      to: isYear,
      months: (months) => months == null || isList(isMonth, 12)(months),
    }) && isOrdered(value),
  datasetOrgIds: isIntegerList,
  projectOrgIds: isIntegerList,
  observationIds: isList(isInteger, MAX_OBSERVATION_IDS),
  withImages: isBoolean,
  changedByAnnotation: isBoolean,
  hiddenObservations: (value) => value === 'Exclude' || value === 'Include' || value === 'Only',
  sensitive: isBoolean,
  uncertainIdentification: isBoolean,
  introduced: isBoolean,
};

/** Saved-filter links were `?filter=<id>` before `?filter=` carried JSON. The ID is a GUID, which JSON never is. */
export const isSavedFilterId = (value: string): boolean => SAVED_FILTER_ID.test(value);

export interface FilterLink {
  filter: ObservationSearchFilter;
  /** Fields that were left out: unknown, the wrong shape, or past a limit the API enforces. */
  dropped: string[];
}

/** Reads the JSON in a `?filter=` link. Returns null when it isn't a JSON object at all. */
export function parseFilterLink(json: string): FilterLink | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    return null;
  }

  const filter: Record<string, unknown> = {};
  const dropped: string[] = [];
  for (const [key, value] of Object.entries(parsed)) {
    if (value === null) continue;
    if (Object.hasOwn(LINK_FILTER_FIELDS, key) && LINK_FILTER_FIELDS[key as LinkFilterField](value)) {
      filter[key] = value;
    } else {
      dropped.push(key);
    }
  }
  return { filter: filter as ObservationSearchFilter, dropped };
}

/** Reads `minX,minY,maxX,maxY` in EPSG:25833. East is negative in western Norway, so only the order is checked. */
export function parseExtent(value: string): MapExtent | null {
  const parts = value.split(',');
  if (parts.length !== 4 || parts.some((part) => part.trim() === '')) return null;
  const [minX, minY, maxX, maxY] = parts.map(Number);
  if (![minX, minY, maxX, maxY].every(Number.isFinite) || minX >= maxX || minY >= maxY) return null;
  return [minX, minY, maxX, maxY];
}
