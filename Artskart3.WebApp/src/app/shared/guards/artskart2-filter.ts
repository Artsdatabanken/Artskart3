import type { MapExtent } from '../services/map-view/map-view.service';
import type { HiddenObservationFilter, ObservationSearchFilter } from '../types/api.types';

/** Artskart 2's period slider ran from 1700 to next year; either end left at its limit meant no limit. */
const ARTSKART2_MIN_YEAR = 1700;
const ARTSKART2_MAX_COORDINATE_PRECISION = 10000;

/**
 * Display settings, `Cons`, which the Artskart 2 server never applied, and what bookmarks store besides
 * the filter. A bookmark's `Tags` repeat its yes/no filters, which are translated instead.
 */
const DISPLAY_ONLY = new Set([
  'Style',
  'CenterPoints',
  'BoundingBox',
  'EpsgCode',
  'Matr',
  'Cons',
  'Id',
  'Name',
  'Tags',
  'GroupBy',
  'IndexFrom',
  'IndexTo',
  'IncludeEmptyCoordinates',
]);

const ID_LISTS = [
  ['TaxonIds', 'taxonIds'],
  ['TaxonGroupIds', 'taxonGroupIds'],
  ['Categories', 'categoryIds'],
  ['BasisOfRecords', 'basisOfRecordIds'],
  ['Behaviors', 'behaviorIds'],
  ['InstitutionIds', 'organizationIds'],
] as const satisfies readonly (readonly [string, keyof ObservationSearchFilter])[];

/** Artskart 2 yes/no filters map to a tag: `[1]` keeps observations with it, `[2]` those without it, `[]` or both mean no filter. */
const TAG_FILTERS = [
  ['Img', 'withImages'],
  ['UnsureId', 'uncertainIdentification'],
  ['Spontan', 'introduced'],
  ['Sensitive', 'sensitive'],
] as const satisfies readonly (readonly [string, keyof ObservationSearchFilter])[];

/** `Found` filters on the "absent" tag and `NotRecovered` on its own tag; Artskart 3 has one status per observation. */
const REGISTRATION_STATUSES = [
  { id: 1, absent: false, notRecovered: false },
  { id: 2, absent: true, notRecovered: false },
  { id: 3, absent: false, notRecovered: true },
];

type TagFilter = 'with' | 'without' | 'any';

export interface Artskart2FilterTranslation {
  filter: ObservationSearchFilter;
  /** Artskart 2 filters that were set but have no Artskart 3 equivalent. */
  dropped: string[];
  /** Artskart 2 `Area.Id`s, which differ from Artskart 3's and are translated through a table. */
  areaIds: number[];
  catalogNumbers: string[];
  /** Collections and datasets, which Artskart 3 filters in different fields depending on the organisation type. */
  collectionIds: number[];
  /** Import sources, which Artskart 3 can only filter through their datasets. */
  nodeIds: number[];
}

/**
 * Translates the JSON after `/filter/` in an Artskart 2 link, or a stored Artskart 2 bookmark. What needs a
 * lookup (areas, catalogue numbers, collections and import sources) is returned separately.
 */
export function translateArtskart2Filter(source: Record<string, unknown>, currentYear: number): Artskart2FilterTranslation {
  const filter: ObservationSearchFilter = {};
  const dropped: string[] = [];
  const handled = new Set<string>(DISPLAY_ONLY);
  const take = (key: string): unknown => {
    handled.add(key);
    return source[key];
  };

  for (const [from, to] of ID_LISTS) {
    const ids = toIntegers(take(from));
    if (ids === null) dropped.push(from);
    else if (ids.length > 0) filter[to] = ids;
  }

  const includeSubTaxa = take('IncludeSubTaxonIds');
  if (includeSubTaxa === false) filter.includeDescendantTaxa = false;
  else if (includeSubTaxa !== undefined && includeSubTaxa !== true) dropped.push('IncludeSubTaxonIds');

  const months = toIntegers(take('Months'));
  const validMonths = months !== null && months.every((month) => month >= 1 && month <= 12);
  if (!validMonths) dropped.push('Months');
  const yearFrom = toInteger(take('YearFrom'));
  const yearTo = toInteger(take('YearTo'));
  if (yearFrom === null) dropped.push('YearFrom');
  if (yearTo === null) dropped.push('YearTo');
  const period = {
    from: yearFrom && yearFrom > ARTSKART2_MIN_YEAR ? yearFrom : undefined,
    to: yearTo && yearTo <= currentYear ? yearTo : undefined,
    months: validMonths && months.length > 0 ? months : undefined,
  };
  if (period.from || period.to || period.months) filter.period = period;

  const precisionFrom = toInteger(take('CoordinatePrecisionFrom'));
  const precisionTo = toInteger(take('CoordinatePrecisionTo'));
  if (precisionFrom === null) dropped.push('CoordinatePrecisionFrom');
  if (precisionTo === null) dropped.push('CoordinatePrecisionTo');
  const coordinatePrecision = {
    from: precisionFrom || undefined,
    to: precisionTo && precisionTo < ARTSKART2_MAX_COORDINATE_PRECISION ? precisionTo : undefined,
  };
  if (coordinatePrecision.from || coordinatePrecision.to) filter.coordinatePrecision = coordinatePrecision;

  const tagFilter = (key: string): TagFilter => {
    const value = toTagFilter(take(key));
    if (value === null) dropped.push(key);
    return value ?? 'any';
  };

  for (const [from, to] of TAG_FILTERS) {
    const value = tagFilter(from);
    if (value !== 'any') filter[to] = value === 'with';
  }

  const found = tagFilter('Found');
  const notRecovered = tagFilter('NotRecovered');
  const matches = (value: TagFilter, hasTag: boolean) => value === 'any' || (value === 'with') === hasTag;
  const statuses = REGISTRATION_STATUSES.filter((s) => matches(found, s.absent) && matches(notRecovered, s.notRecovered));
  if (statuses.length === 1) {
    filter.registrationStatusId = statuses[0].id;
  } else if (found === 'without' && notRecovered === 'any') {
    // Artskart 2 labelled this "show found". It also kept not-recovered records, which are rare.
    filter.registrationStatusId = 1;
  } else if (statuses.length < REGISTRATION_STATUSES.length) {
    if (found !== 'any') dropped.push('Found');
    if (notRecovered !== 'any') dropped.push('NotRecovered');
  }

  // Artskart 3 has one choice for both: all, without hidden (the default), only hidden, or changed but not hidden.
  const blocked = tagFilter('Blocked');
  const annotated = tagFilter('Annotated');
  if (blocked === 'without' && annotated === 'with') {
    filter.changedByAnnotation = true;
  } else {
    if (annotated !== 'any') dropped.push('Annotated');
    const hidden: Partial<Record<TagFilter, HiddenObservationFilter>> = { with: 'Only', any: 'Include' };
    if (hidden[blocked]) filter.hiddenObservations = hidden[blocked];
  }

  for (const key of ['Errors', 'Valid']) {
    if (tagFilter(key) !== 'any') dropped.push(key);
  }

  const geometry = take('Geometry');
  if (typeof geometry === 'string' && geometry.trim()) filter.customAreaWkt = geometry;
  else if (!isEmpty(geometry)) dropped.push('Geometry');

  const areaIds = toIntegers(take('AreaIds'));
  if (areaIds === null) dropped.push('AreaIds');

  const collectionIds = toIntegers(take('CollectionIds'));
  if (collectionIds === null) dropped.push('CollectionIds');

  // A negative ID meant "not from these sources", which no Artskart 3 filter can express.
  const nodeIds = toIntegers(take('NodeIds'));
  if (nodeIds === null || nodeIds.some((id) => id < 0)) dropped.push('NodeIds');

  const catalogNumbers = take('CatalogNumbers');
  const validCatalogNumbers = Array.isArray(catalogNumbers) && catalogNumbers.every((n) => typeof n === 'string' || typeof n === 'number');
  if (!validCatalogNumbers && !isEmpty(catalogNumbers)) dropped.push('CatalogNumbers');

  for (const [key, value] of Object.entries(source)) {
    if (!handled.has(key) && !isEmpty(value)) dropped.push(key);
  }

  return {
    filter,
    dropped: [...new Set(dropped)],
    areaIds: areaIds ?? [],
    catalogNumbers: validCatalogNumbers ? catalogNumbers.map(String).filter(Boolean) : [],
    collectionIds: collectionIds ?? [],
    nodeIds: nodeIds && !nodeIds.some((id) => id < 0) ? nodeIds : [],
  };
}

/** Artskart 2 left out empty lists, zeros and `false`, so an empty value means the filter isn't set. */
function isEmpty(value: unknown): boolean {
  return value === undefined || value === null || value === '' || value === 0 || value === false || (Array.isArray(value) && value.length === 0);
}

// Artskart 2 wrote IDs as numbers or numeric strings, depending on where they came from.
function toNumber(value: unknown): number | null {
  if (typeof value === 'string' && value.trim() === '') return null;
  const number = typeof value === 'number' || typeof value === 'string' ? Number(value) : NaN;
  return Number.isInteger(number) ? number : null;
}

function toInteger(value: unknown): number | null | undefined {
  return isEmpty(value) ? undefined : toNumber(value);
}

function toIntegers(value: unknown): number[] | null {
  if (isEmpty(value)) return [];
  if (!Array.isArray(value)) return null;
  const numbers = value.map(toNumber);
  return numbers.every((n) => n !== null) ? (numbers as number[]) : null;
}

function toTagFilter(value: unknown): TagFilter | null {
  const selected = toIntegers(value);
  if (selected === null || selected.some((option) => option !== 1 && option !== 2)) return null;
  const options = new Set(selected);
  if (options.size !== 1) return 'any';
  return options.has(1) ? 'with' : 'without';
}

/** Artskart 2's map grid: EPSG:32633 over [-2500000, 3500000, 3045984, 9045984] in 256-pixel tiles, zoom 0 to 18. */
const ARTSKART2_ZOOM_0_RESOLUTION = 5545984 / 256;
const ARTSKART2_MAX_ZOOM = 18;
const ARTSKART2_DEFAULT_CENTER = '427864,7623020';
const ARTSKART2_DEFAULT_ZOOM = 3;

/** The map size the area is worked out for. Artskart 3 fits the area to its screen, so this only sets how much shows. */
const MAP_WIDTH_PX = 1280;
const MAP_HEIGHT_PX = 800;

/**
 * The area an Artskart 2 link showed, from the centre (east,north in UTM 33) and zoom level in its path, in whole
 * metres. Null for Artskart 2's default view, which Artskart 3 replaces with its own, and for values that aren't a view.
 */
export function translateArtskart2View(center: string, zoom: string): MapExtent | null {
  const level = Number(zoom);
  if (center === ARTSKART2_DEFAULT_CENTER && level === ARTSKART2_DEFAULT_ZOOM) return null;

  const coordinates = center.split(',');
  const [east, north] = coordinates.map(Number);
  if (coordinates.length !== 2 || coordinates.some((c) => c.trim() === '') || !Number.isFinite(east) || !Number.isFinite(north)) return null;
  if (zoom.trim() === '' || !Number.isFinite(level) || level < 0 || level > ARTSKART2_MAX_ZOOM) return null;

  const resolution = ARTSKART2_ZOOM_0_RESOLUTION / 2 ** level;
  const halfWidth = (resolution * MAP_WIDTH_PX) / 2;
  const halfHeight = (resolution * MAP_HEIGHT_PX) / 2;
  return [east - halfWidth, north - halfHeight, east + halfWidth, north + halfHeight].map(Math.round) as MapExtent;
}
