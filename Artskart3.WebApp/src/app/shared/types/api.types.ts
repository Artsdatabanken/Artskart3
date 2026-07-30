import type { components } from './api.generated';

export type ObservationDto = components['schemas']['ObservationDto'];
export type PagedObservationResponse = components['schemas']['PagedObservationResponseDto'];
export type CategoryTypeDto = components['schemas']['CategoryTypeDto'];
export type CategoryDto = components['schemas']['CategoryDto'];
export type AreaResponseDto = components['schemas']['AreaResponseDto'];
export type AreaTypeDto = components['schemas']['AreaTypeDto'];
export type AreaDto = components['schemas']['AreaDto'];
export type CoordinatePrecisionDto = components['schemas']['CoordinatePrecisionDto'];
export type InstitutionDto = components['schemas']['InstitutionDto'];
export type BehaviorDto = components['schemas']['BehaviorDto'];
export type BasisOfRecordDto = components['schemas']['BasisOfRecordDto'];
export type TaxonGroupDto = components['schemas']['TaxonGroupDto'];
export type CsvExportJobDto = components['schemas']['CsvExportJobDto'];
export type StartExportRequestDto = components['schemas']['StartExportRequestDto'];
export type ExportSummaryDto = components['schemas']['ExportSummaryDto'];
export type ObservationSearchFilter = components['schemas']['ObservationSearchFilterDto'];
export type ObservationListInfoDto = components['schemas']['ObservationListInfoDto'];

export type SpeciesDto = components['schemas']['SpeciesDto'];
export type VernacularNameDto = components['schemas']['VernacularNameDto'];

// Export status enum
export const CSV_EXPORT_STATUS = {
  Pending: 0,
  Processing: 1,
  Complete: 2,
  Failed: 3,
  Cancelled: 4,
} as const;

export type CsvExportStatus = (typeof CSV_EXPORT_STATUS)[keyof typeof CSV_EXPORT_STATUS];

export interface TaxonTreeNodeDto {
  id: number;
  validScientificName?: string | null;
  preferredPopularName?: string | null;
  taxonRankId: number;
  taxonGroupId: number;
  cumulativeObservationCount?: number | null;
  existsInCountry: boolean;
  hasChildren: boolean;
  children: TaxonTreeNodeDto[];
}

/** Foreldrekjeden for et taxon, fra rotnivå til nærmeste forelder. */
export type TaxonAncestryDto = components['schemas']['TaxonAncestryDto'];
export type TaxonAncestryLevelDto = components['schemas']['TaxonAncestryLevelDto'];

