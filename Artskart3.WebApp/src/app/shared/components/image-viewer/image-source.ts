import { ObservationMediaDto } from '@shared/types/api.types';

export interface ViewerImage {
  src: string;
  alt: string;
  popularName?: string | null;
  scientificName?: string | null;
  author?: string | null;
  description?: string | null;
  rightsHolder?: string | null;
  license?: string | null;
}

export type SpeciesNames = Pick<ViewerImage, 'popularName' | 'scientificName' | 'author'>;

export function webUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return (url.protocol === 'https:' || url.protocol === 'http:') && !url.username && !url.password ? url.href : null;
  } catch {
    return null;
  }
}

/** The nearest item from `current` in the given direction that passes `include`, or null at the end of the list. */
export function neighbour<T>(items: readonly T[], current: T, step: 1 | -1, include: (item: T) => boolean): T | null {
  const start = items.indexOf(current);
  if (start < 0) return null;
  for (let i = start + step; i >= 0 && i < items.length; i += step) {
    if (include(items[i])) return items[i];
  }
  return null;
}

/** The origin URL first, then the copy stored in the database, skipping sources that already failed to load. */
export function imageSource(observationId: number, image: ObservationMediaDto, failed: readonly string[] = []): string | null {
  const origin = webUrl(image.origin);
  if (origin && !failed.includes(origin)) return origin;
  const stored = `/api/observations/${observationId}/media/${image.id}/image`;
  return image.id != null && image.hasStoredImage && !failed.includes(stored) ? stored : null;
}
