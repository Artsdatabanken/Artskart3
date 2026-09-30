import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PlaceSearchComponent } from './place-search.component';
import { PlaceSearchService } from '../../services/place-search/place-search.service';
import { PlaceSearchResultDto } from '../../types/api.types';

describe('PlaceSearchComponent', () => {
  let component: PlaceSearchComponent;
  let fixture: ComponentFixture<PlaceSearchComponent>;
  let httpTesting: HttpTestingController;

  const mockPlaces: PlaceSearchResultDto[] = [
    {
      stedsNummer: 307915,
      name: 'Oslo',
      navneObjektType: 'By',
      recommendedZoom: 3,
      east: 261000,
      north: 6649000,
      coordinateSystem: 25833,
      municipalities: ['Oslo'],
      counties: ['Oslo'],
      alternativeNames: [],
    },
    {
      stedsNummer: 42,
      name: 'Oslofjorden',
      navneObjektType: 'Fjord',
      recommendedZoom: 3,
      east: 270000,
      north: 6600000,
      coordinateSystem: 25833,
      municipalities: [],
      counties: ['Vestfold'],
      alternativeNames: [],
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlaceSearchComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceSearchComponent);
    component = fixture.componentInstance;
    httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should hide autocomplete when input is less than 2 characters', () => {
    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'o' } }) as unknown as Event);
    expect(component.showAutocomplete()).toBe(false);
    expect(component.placeResults().length).toBe(0);
  });

  it('should hide autocomplete and clear results on clear', () => {
    component.placeResults.set(mockPlaces);
    component.showAutocomplete.set(true);
    component.searchTerm.set('os');

    component.onSearchClear();

    expect(component.showAutocomplete()).toBe(false);
    expect(component.placeResults().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should emit placeSelected and reset state on select', () => {
    const emitted: PlaceSearchResultDto[] = [];
    component.placeSelected.subscribe((place) => emitted.push(place));
    component.placeResults.set(mockPlaces);
    component.showAutocomplete.set(true);
    component.searchTerm.set('os');

    component.onPlaceSelect(mockPlaces[0]);

    expect(emitted).toEqual([mockPlaces[0]]);
    expect(component.showAutocomplete()).toBe(false);
    expect(component.placeResults().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should fetch and display results for a valid search term', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(PlaceSearchService);
      vi.spyOn(service, 'searchPlaces').mockReturnValue(of(mockPlaces));

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Oslo' } }) as unknown as Event);
      vi.advanceTimersByTime(300);

      expect(component.placeResults()).toEqual(mockPlaces);
      expect(component.showAutocomplete()).toBe(true);
      expect(component.showNoResults()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should show no-results state when service returns empty list', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(PlaceSearchService);
      vi.spyOn(service, 'searchPlaces').mockReturnValue(of([]));

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Ukjentsted' } }) as unknown as Event);
      vi.advanceTimersByTime(300);

      expect(component.showAutocomplete()).toBe(false);
      expect(component.showNoResults()).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should swallow errors from the search service and show no results', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(PlaceSearchService);
      vi.spyOn(service, 'searchPlaces').mockReturnValue(throwError(() => new Error('network error')));

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Oslo' } }) as unknown as Event);
      vi.advanceTimersByTime(300);

      expect(component.showAutocomplete()).toBe(false);
      expect(component.showNoResults()).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  describe('getPrimaryLocation', () => {
    it('prefers municipalities over counties', () => {
      expect(component.getPrimaryLocation(mockPlaces[0])).toBe('Oslo');
    });

    it('falls back to counties when no municipalities exist', () => {
      expect(component.getPrimaryLocation(mockPlaces[1])).toBe('Vestfold');
    });

    it('returns empty string when neither is present', () => {
      const place = { ...mockPlaces[0], municipalities: [], counties: [] };
      expect(component.getPrimaryLocation(place)).toBe('');
    });
  });

  describe('highlightMatch', () => {
    it('wraps matching term in strong tags', () => {
      component.searchTerm.set('Oslo');
      expect(component.highlightMatch('Oslo')).toBe('<strong>Oslo</strong>');
    });

    it('returns text unchanged when there is no search term', () => {
      component.searchTerm.set('');
      expect(component.highlightMatch('Oslo')).toBe('Oslo');
    });

    it('returns empty string for null/undefined text', () => {
      expect(component.highlightMatch(null)).toBe('');
      expect(component.highlightMatch(undefined)).toBe('');
    });
  });

  describe('keyboard navigation', () => {
    it('moves highlight down with ArrowDown and wraps around', () => {
      component.placeResults.set(mockPlaces);
      component.showAutocomplete.set(true);
      component.highlightedIndex.set(-1);

      component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
      expect(component.highlightedIndex()).toBe(0);

      component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
      expect(component.highlightedIndex()).toBe(1);

      component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
      expect(component.highlightedIndex()).toBe(0);
    });

    it('moves highlight up with ArrowUp and wraps around', () => {
      component.placeResults.set(mockPlaces);
      component.showAutocomplete.set(true);
      component.highlightedIndex.set(0);

      component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowUp' }));
      expect(component.highlightedIndex()).toBe(1);
    });

    it('dismisses the popup on Escape', () => {
      component.placeResults.set(mockPlaces);
      component.showAutocomplete.set(true);

      component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));

      expect(component.showAutocomplete()).toBe(false);
      expect(component.highlightedIndex()).toBe(-1);
    });
  });
});
