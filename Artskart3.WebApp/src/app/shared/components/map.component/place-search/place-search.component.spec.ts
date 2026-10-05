import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError, timer, map } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PlaceSearchResult } from '../../../types/place-search.types';
import { PlaceSearchService } from '../../../services/place-search/place-search.service';
import { PlaceSearchComponent } from './place-search.component';

describe('PlaceSearchComponent', () => {
  let fixture: ComponentFixture<PlaceSearchComponent>;
  let component: PlaceSearchComponent;
  let searchService: PlaceSearchService;

  const places: PlaceSearchResult[] = [
    {
      id: 'alta-fjord-kvaenangen',
      name: 'Alta',
      placeType: 'Fjord',
      municipalities: ['Kvænangen'],
      counties: ['Troms'],
      coordinates: [768586.78, 7781425.05],
      coordinateSystem: 'EPSG:25833',
      zoomLevel: 1,
    },
    {
      id: 'alta-city',
      name: 'Alta',
      placeType: 'By',
      municipalities: ['Alta'],
      counties: ['Finnmark'],
      coordinates: [815288.9, 7783951.43],
      coordinateSystem: 'EPSG:25833',
      zoomLevel: 2,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlaceSearchComponent, TranslateModule.forRoot()],
      providers: [PlaceSearchService],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceSearchComponent);
    component = fixture.componentInstance;
    searchService = TestBed.inject(PlaceSearchService);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows matching results with disambiguating location and type', async () => {
    vi.useFakeTimers();
    vi.spyOn(searchService, 'searchPlaces').mockReturnValue(of(places));

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);
    fixture.detectChanges();

    expect(component.showResults()).toBe(true);
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Kvænangen, Troms');
    expect(fixture.nativeElement.textContent).toContain('Fjord');
  });

  it('loads mock suggestions from the adb-search input event', async () => {
    vi.useFakeTimers();
    const search = vi.spyOn(searchService, 'searchPlaces').mockReturnValue(of(places));
    const searchElement = fixture.nativeElement.querySelector('adb-search');

    searchElement.dispatchEvent(
      new CustomEvent('adb-input', {
        detail: { value: 'Alta' },
        bubbles: true,
        composed: true,
      }),
    );
    await vi.advanceTimersByTimeAsync(250);
    fixture.detectChanges();

    expect(search).toHaveBeenCalledWith('Alta');
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
  });

  it('runs the pending query immediately when the search button is pressed', async () => {
    vi.useFakeTimers();
    const search = vi.spyOn(searchService, 'searchPlaces').mockReturnValue(of(places));
    const searchElement = fixture.nativeElement.querySelector('adb-search');

    searchElement.dispatchEvent(
      new CustomEvent('adb-input', {
        detail: { value: 'Alta' },
        bubbles: true,
        composed: true,
      }),
    );
    fixture.detectChanges();
    searchElement.dispatchEvent(new CustomEvent('adb-search', { bubbles: true, composed: true }));
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();

    expect(search).toHaveBeenCalledWith('Alta');
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
  });

  it('shows the no-results state for an empty response', async () => {
    vi.useFakeTimers();
    vi.spyOn(searchService, 'searchPlaces').mockReturnValue(of([]));

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Unknown' } }));
    await vi.advanceTimersByTimeAsync(250);

    expect(component.showNoResults()).toBe(true);
    expect(component.showResults()).toBe(false);
  });

  it('shows the error state when the search fails', async () => {
    vi.useFakeTimers();
    vi.spyOn(searchService, 'searchPlaces').mockReturnValue(throwError(() => new Error('offline')));

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);

    expect(component.hasError()).toBe(true);
    expect(component.showNoResults()).toBe(false);
  });

  it('cancels a pending search when the field is cleared', async () => {
    vi.useFakeTimers();
    const search = vi.spyOn(searchService, 'searchPlaces').mockReturnValue(timer(100).pipe(map(() => places)));

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);
    component.onSearchClear();
    await vi.advanceTimersByTimeAsync(200);

    expect(search).toHaveBeenCalledTimes(1);
    expect(component.results()).toEqual([]);
    expect(component.showResults()).toBe(false);
  });

  it('emits the selected place and clears the search state', () => {
    const selected: PlaceSearchResult[] = [];
    component.placeSelected.subscribe((place) => selected.push(place));
    component.results.set(places);
    component.showResults.set(true);
    component.searchTerm.set('Alta');

    component.onPlaceSelect(places[0]);

    expect(selected).toEqual([places[0]]);
    expect(component.results()).toEqual([]);
    expect(component.searchTerm()).toBe('');
    expect(component.showResults()).toBe(false);
  });
});