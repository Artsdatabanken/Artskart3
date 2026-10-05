import { Injectable } from '@angular/core';
import { Observable, delay, map, of } from 'rxjs';
import { PlaceSearchResult } from '../../types/place-search.types';
import { PLACE_SEARCH_MOCK_DATA } from './place-search.mock-data';

@Injectable({
  providedIn: 'root',
})
export class PlaceSearchService {
  searchPlaces(query: string): Observable<PlaceSearchResult[]> {
    const normalizedQuery = query.trim().toLocaleLowerCase('nb-NO');
    if (!normalizedQuery) return of([]);

    return of(PLACE_SEARCH_MOCK_DATA).pipe(
      delay(180),
      map((places) =>
        places.filter((place) =>
          [place.name, ...place.municipalities, ...place.counties].some((value) =>
            value.toLocaleLowerCase('nb-NO').startsWith(normalizedQuery),
          ),
        ),
      ),
    );
  }
}