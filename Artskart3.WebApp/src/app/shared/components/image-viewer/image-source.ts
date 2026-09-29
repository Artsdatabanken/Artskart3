export interface ViewerImage {
  src: string;
  alt: string;
  description?: string | null;
  rightsHolder?: string | null;
  license?: string | null;
}

export function webUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return (url.protocol === 'https:' || url.protocol === 'http:') && !url.username && !url.password ? url.href : null;
  } catch {
    return null;
  }
}
