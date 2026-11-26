/** Several collectors are stored as one value separated by `|`. */
export function formatCollectors(collector: string | null | undefined): string {
  if (!collector) return '';
  return collector
    .split('|')
    .map((name) => name.trim())
    .filter(Boolean)
    .join(', ');
}
