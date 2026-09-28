import { CATEGORY_ORDER } from '@shared/constants/category-order.const';
import { ObservationListInfoDto } from '@shared/types/api.types';

export type ObservationGrouping = 'taxonGroup' | 'location' | 'redList' | 'alienSpecies';
export type RegistrationStatus = 'found' | 'notRecovered' | 'absent';
export type ObservationRequestState = 'loading' | 'ready' | 'error';

export interface ObservationSelection {
  readonly key: number;
  readonly locationIds: readonly number[];
  readonly kind: 'point' | 'polygon' | 'selection';
  readonly geometryLabel: string;
}

export interface CategoryBadge {
  readonly code: string;
  readonly label: string;
}

export interface ObservationTreeNode {
  readonly id: string;
  readonly kind: 'group' | 'status' | 'species' | 'observation';
  readonly label: string;
  readonly count: number;
  readonly scientific: boolean;
  readonly badges: readonly CategoryBadge[];
  readonly status?: RegistrationStatus;
  readonly children: readonly ObservationTreeNode[];
}

const STATUS_ORDER: RegistrationStatus[] = ['found', 'notRecovered', 'absent'];
const CATEGORY_TYPES = { redList: 1, alienSpecies: 2 } as const;
type TranslateLabel = (key: string) => string;

export function registrationStatus(observation: ObservationListInfoDto): RegistrationStatus {
  if (observation.registrationType?.includes('Absent')) return 'absent';
  return observation.registrationType?.includes('NotRecovered') ? 'notRecovered' : 'found';
}

export function hasUnknownCategory(observation: ObservationListInfoDto): boolean {
  return !!observation.categoryCode &&
    (!CATEGORY_ORDER.includes(observation.categoryCode) ||
      (observation.categoryTypeId !== 1 && observation.categoryTypeId !== 2));
}

export function buildObservationTree(
  observations: readonly ObservationListInfoDto[],
  mode: ObservationGrouping,
  language: string,
  translate: TranslateLabel,
  formatDate: (value: string | null | undefined) => string,
): ObservationTreeNode[] {
  const t = (key: string) => translate(`observationList.${key}`);
  const collator = new Intl.Collator(language === 'no' ? 'nb-NO' : 'en-GB');
  const categoryLabel = (code: string) => t(`categories.${code}`);
  const categoryRank = (code: string) => CATEGORY_ORDER.indexOf(code);
  const badges = (records: readonly ObservationListInfoDto[]): CategoryBadge[] =>
    [...new Set(records.map((o) => o.categoryCode).filter((code): code is string => !!code))]
      .sort((a, b) => categoryRank(a) - categoryRank(b) || collator.compare(a, b))
      .map((code) => ({
        code,
        label: CATEGORY_ORDER.includes(code) ? categoryLabel(code) : `${t('unknownCategory')} (${code})`,
      }));
  const groupBy = (records: readonly ObservationListInfoDto[], key: (o: ObservationListInfoDto) => string) => {
    const groups = new Map<string, ObservationListInfoDto[]>();
    for (const record of records) {
      const id = key(record);
      const group = groups.get(id);
      if (group) group.push(record);
      else groups.set(id, [record]);
    }
    return groups;
  };
  const topKey = (o: ObservationListInfoDto): string => {
    if (mode === 'taxonGroup') return String(o.taxonGroupId ?? 'unknown');
    if (mode === 'location') return String(o.locationId ?? 'unknown');
    return o.categoryCode && !hasUnknownCategory(o) && o.categoryTypeId === CATEGORY_TYPES[mode]
      ? o.categoryCode
      : mode === 'redList' ? 'NE' : 'NR';
  };
  const topLabel = (key: string, first: ObservationListInfoDto): string => {
    if (mode === 'taxonGroup') return first.taxonGroupName?.trim() || t('unknownTaxonGroup');
    if (mode === 'location') return first.locality?.trim() || t('unknownLocality');
    return categoryLabel(key);
  };
  const compareNodes = (a: ObservationTreeNode, b: ObservationTreeNode) =>
    collator.compare(a.label, b.label) || collator.compare(a.id, b.id);
  const dateValue = (value: string | null | undefined): number => {
    const timestamp = value ? Date.parse(value) : NaN;
    return Number.isNaN(timestamp) ? -Infinity : timestamp;
  };
  return [...groupBy(observations, topKey)].map(([key, records]): ObservationTreeNode => {
    const id = `${mode}/${key}`;
    const first = records[0];
    const statuses = groupBy(records, registrationStatus);
    return {
      id, kind: 'group', label: topLabel(key, first), count: records.length, scientific: false,
      badges: (mode === 'redList' || mode === 'alienSpecies') && CATEGORY_ORDER.includes(key)
        ? [{ code: key, label: categoryLabel(key) }] : [],
      children: STATUS_ORDER.filter((status) => statuses.has(status)).map((status) => {
        const statusRecords = statuses.get(status)!;
        const statusId = `${id}/${status}`;
        return {
          id: statusId, kind: 'status', status, label: t(status), count: statusRecords.length,
          scientific: false, badges: badges(statusRecords),
          children: [...groupBy(statusRecords, (o) => String(o.taxonId ?? `unknown-${o.id}`))]
            .map(([taxonId, speciesRecords]): ObservationTreeNode => {
              const species = speciesRecords[0];
              const speciesId = `${statusId}/${taxonId}`;
              const displayName = species.displayName?.trim();
              const popularName = species.preferredPopularName?.trim();
              const scientificName = species.scientificName?.trim();
              return {
                id: speciesId, kind: 'species', count: speciesRecords.length,
                label: displayName || popularName || scientificName || t('unknownSpecies'),
                scientific: !popularName && !!scientificName, badges: badges(speciesRecords),
                children: [...speciesRecords]
                  .sort((a, b) => {
                    const aDate = dateValue(a.dateTimeCollected);
                    const bDate = dateValue(b.dateTimeCollected);
                    return (aDate === bDate ? 0 : aDate > bDate ? -1 : 1) || (b.id ?? 0) - (a.id ?? 0);
                  })
                  .map((o) => ({
                    id: `${speciesId}/${o.id}`, kind: 'observation', status,
                    label: `${formatDate(o.dateTimeCollected) || t('unknownDate')}: ${o.collector?.trim() || t('unknownCollector')}`,
                    count: 1, scientific: false, badges: [], children: [],
                  })),
              };
            }).sort(compareNodes),
        };
      }),
    };
  }).sort((a, b) => {
    if (mode === 'taxonGroup' || mode === 'location') return compareNodes(a, b);
    const rank = (node: ObservationTreeNode) => node.badges.length
      ? categoryRank(node.badges[0].code) : CATEGORY_ORDER.length;
    return rank(a) - rank(b) || compareNodes(a, b);
  });
}
