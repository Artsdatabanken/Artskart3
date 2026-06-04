/**
 * Artskart3 API Constants and Messages
 * Centralized strings and constants for API interactions
 */

export const ApiMessages = {
  Errors: {
    GeoJsonParseError: 'Failed to parse GeoJSON response from server. Please try again.',
    InvalidParameters: 'Invalid search parameters provided.',
    NetworkError: 'Network error occurred. Please check your connection and try again.',
    Unauthorized: 'You are not authorized to access this resource.',
    Forbidden: 'Access to this resource is forbidden.',
    ApiUnavailable: 'The API service is currently unavailable. Please try again later.'
  }
} as const;

export const ZoomLevelMapping = {
  MIN_ZOOM: 1,
  MAX_ZOOM: 15
} as const;

export const RetryConfig = {
  MaxAttempts: 3,
  InitialDelayMs: 1000
} as const;
