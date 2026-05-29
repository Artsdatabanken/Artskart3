import type { components } from './api.generated';

// Response DTOs
export type ObservationDto = components['schemas']['ObservationDto'];
export type PagedObservationResponse = components['schemas']['PagedObservationResponseDto'];
export type CategoryTypeDto = components['schemas']['CategoryTypeDto'];
export type CategoryDto = components['schemas']['CategoryDto'];

// Request DTOs
export type ObservationSearchFilter = components['schemas']['ObservationSearchFilterDto'];
