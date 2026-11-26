import { originalLinkKey } from './original-site';

describe('originalLinkKey', () => {
  it('names the site from the domain, including subdomains', () => {
    expect(originalLinkKey('https://mobil.artsobservasjoner.no/sighting/5299619')).toBe(
      'observationDetails.originalLink.artsobservasjoner',
    );
    expect(originalLinkKey('https://www.gbif.org/occurrence/1931476664')).toBe('observationDetails.originalLink.gbif');
    expect(originalLinkKey('https://faktaark.naturbase.no/?id=BN1')).toBe('observationDetails.originalLink.naturbase');
    expect(originalLinkKey('http://www.miljolare.no/data/ut/1')).toBe('observationDetails.originalLink.miljolare');
  });

  it('falls back to a generic text for other or lookalike hosts', () => {
    expect(originalLinkKey('https://www.inaturalist.org/observations/1')).toBe('observationDetails.originalLink.other');
    expect(originalLinkKey('https://notgbif.org/occurrence/1')).toBe('observationDetails.originalLink.other');
    expect(originalLinkKey('not a url')).toBe('observationDetails.originalLink.other');
  });
});
