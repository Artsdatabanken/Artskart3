import type { components } from './api.generated';

// Response DTOs
export type ObservationDto = components['schemas']['ObservationDto'];
export type PagedObservationResponse = components['schemas']['PagedObservationResponseDto'];
export type CategoryTypeDto = components['schemas']['CategoryTypeDto'];
export type CategoryDto = components['schemas']['CategoryDto'];
export type AreaTypeDto = components['schemas']['AreaTypeDto'];
export type AreaDto = components['schemas']['AreaDto'];
export type CoordinatePrecisionDto = components['schemas']['CoordinatePrecisionDto'];
export type InstitutionDto = components['schemas']['InstitutionDto'];
export type BehaviorDto = components['schemas']['BehaviorDto'];

// Request DTOs
export type ObservationSearchFilter = components['schemas']['ObservationSearchFilterDto'];
