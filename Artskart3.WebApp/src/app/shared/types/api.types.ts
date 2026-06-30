import type { components } from './api.generated';

export type ObservationDto = components['schemas']['ObservationDto'];
export type PagedObservationResponse = components['schemas']['PagedObservationResponseDto'];
export type CategoryTypeDto = components['schemas']['CategoryTypeDto'];
export type CategoryDto = components['schemas']['CategoryDto'];
export type AreaResponseDto = components['schemas']['AreaResponseDto'];
export type AreaTypeDto = components['schemas']['AreaTypeDto'];
export type CountyDto = components['schemas']['CountyDto'];
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
export type NotificationModel = components['schemas']['NotificationModel'];

// Export status enum
export const CSV_EXPORT_STATUS = {
  Pending: 0,
  Processing: 1,
  Complete: 2,
  Failed: 3,
  Cancelled: 4,
} as const;

export type CsvExportStatus = (typeof CSV_EXPORT_STATUS)[keyof typeof CSV_EXPORT_STATUS];
