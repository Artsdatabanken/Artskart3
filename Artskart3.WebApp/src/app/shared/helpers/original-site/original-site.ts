/** Sites that host almost all original observation links, by domain. Subdomains match too, as in mobil.artsobservasjoner.no. */
const SITES: readonly (readonly [domain: string, site: string])[] = [
  ['artsobservasjoner.no', 'artsobservasjoner'],
  ['gbif.org', 'gbif'],
  ['vannmiljo.miljodirektoratet.no', 'vannmiljo'],
  ['naturbase.no', 'naturbase'],
  ['miljolare.no', 'miljolare'],
  ['boldsystems.org', 'bold'],
  ['skandobs.no', 'skandobs'],
];

/** Translation key for a link to the original observation, naming the site it opens, or generic for other sites. */
export function originalLinkKey(url: string): string {
  let host: string;
  try {
    host = new URL(url).hostname;
  } catch {
    host = '';
  }
  const site = SITES.find(([domain]) => host === domain || host.endsWith(`.${domain}`))?.[1] ?? 'other';
  return `observationDetails.originalLink.${site}`;
}
