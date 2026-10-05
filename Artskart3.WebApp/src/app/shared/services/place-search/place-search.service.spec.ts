import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { PlaceSearchService } from './place-search.service';

describe('PlaceSearchService', () => {
  const service = new PlaceSearchService();

  it('returns matching places and preserves duplicate names for disambiguation', async () => {
    const results = await firstValueFrom(service.searchPlaces('alta'));

    expect(results).toHaveLength(2);
    expect(results.map((place) => place.municipalities[0])).toEqual(['Kvænangen', 'Alta']);
  });

  it('returns no results for an unmatched query', async () => {
    const results = await firstValueFrom(service.searchPlaces('not-a-place'));

    expect(results).toEqual([]);
  });

  it('returns no results for a blank query', async () => {
    const results = await firstValueFrom(service.searchPlaces('  '));

    expect(results).toEqual([]);
  });
});