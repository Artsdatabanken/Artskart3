export type RegistrationStatus = 'found' | 'notRecovered' | 'absent';

/** Not recovered is the more specific tag, so it wins when a record has both, as in the API. */
export function registrationStatusFromTags(tags: readonly string[] | null | undefined): RegistrationStatus {
  if (tags?.includes('NotRecovered')) return 'notRecovered';
  return tags?.includes('Absent') ? 'absent' : 'found';
}

/** Ids from the locations endpoint, the same as the registration status filter. */
export function registrationStatusFromId(id: unknown): RegistrationStatus {
  if (id === 2) return 'absent';
  return id === 3 ? 'notRecovered' : 'found';
}
