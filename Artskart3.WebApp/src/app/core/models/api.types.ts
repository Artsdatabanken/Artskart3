export interface ApiErrorResponse {
  error: string;
  details?: string;
  timestamp?: string;
  traceId?: string;
}

export function isApiErrorResponse(response: unknown): response is ApiErrorResponse {
  return (
    response !== null &&
    typeof response === 'object' &&
    'error' in response &&
    typeof (response as Record<string, unknown>)['error'] === 'string'
  );
}
