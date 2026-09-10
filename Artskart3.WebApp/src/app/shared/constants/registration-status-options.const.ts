export interface RegistrationStatusOption {
  id: number | null;
  labelKey: string;
  descriptionKey?: string;
}

/** Radiovalgene for registreringsstatus. `id: null` er standardvalget og gir ingen chip. */
export const REGISTRATION_STATUS_OPTIONS: readonly RegistrationStatusOption[] = [
  { id: null, labelKey: 'sidebar.registreringStatus.alle' },
  { id: 1, labelKey: 'sidebar.registreringStatus.present', descriptionKey: 'sidebar.registreringStatus.presentDescription' },
  { id: 2, labelKey: 'sidebar.registreringStatus.absent', descriptionKey: 'sidebar.registreringStatus.absentDescription' },
  { id: 3, labelKey: 'sidebar.registreringStatus.notrefound', descriptionKey: 'sidebar.registreringStatus.notrefoundDescription' },
];
