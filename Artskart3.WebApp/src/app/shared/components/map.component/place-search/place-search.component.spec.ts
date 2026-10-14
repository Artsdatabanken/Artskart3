import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PlaceSearchResult } from '@artsdatabanken/nbic-map-component';
import { PlaceSearchComponent } from './place-search.component';

describe('PlaceSearchComponent', () => {
  let fixture: ComponentFixture<PlaceSearchComponent>;
  let component: PlaceSearchComponent;
  let search: ReturnType<typeof vi.fn<(query: string, signal: AbortSignal) => Promise<PlaceSearchResult[]>>>;

  const places: PlaceSearchResult[] = [
    {
      id: 'alta-fjord-kvaenangen',
      placeId: '10001',
      name: 'Alta',
      language: 'Norsk',
      type: 'Fjord',
      municipalities: ['Kvænangen'],
      counties: ['Troms'],
      coordinates: { lat: 70, lon: 22.05 },
      zoomLevel: 8.5,
    },
    {
      id: 'alta-city',
      placeId: '10002',
      name: 'Alta',
      language: 'Norsk',
      type: 'By',
      municipalities: ['Alta'],
      counties: ['Finnmark'],
      coordinates: { lat: 69.97, lon: 23.27 },
      zoomLevel: 11,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlaceSearchComponent, TranslateModule.forRoot()],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceSearchComponent);
    component = fixture.componentInstance;
    search = vi.fn<(query: string, signal: AbortSignal) => Promise<PlaceSearchResult[]>>().mockResolvedValue(places);
    fixture.componentRef.setInput('search', search);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows matching results with disambiguating location and type', async () => {
    vi.useFakeTimers();

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);
    fixture.detectChanges();

    expect(component.showResults()).toBe(true);
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Norsk · Kvænangen, Troms');
    expect(fixture.nativeElement.textContent).toContain('Fjord');
  });

  it('loads suggestions from the adb-search input event', async () => {
    vi.useFakeTimers();
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

    expect(search).toHaveBeenCalledWith('Alta', expect.any(AbortSignal));
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
  });

  it('runs the pending query immediately when the search button is pressed', async () => {
    vi.useFakeTimers();
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

    expect(search).toHaveBeenCalledWith('Alta', expect.any(AbortSignal));
    expect(fixture.nativeElement.querySelectorAll('[role="option"]')).toHaveLength(2);
  });

  it('shows the no-results state for an empty response', async () => {
    vi.useFakeTimers();
    search.mockResolvedValue([]);

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Unknown' } }));
    await vi.advanceTimersByTimeAsync(250);

    expect(component.showNoResults()).toBe(true);
    expect(component.showResults()).toBe(false);
  });

  it('shows the error state when the search fails', async () => {
    vi.useFakeTimers();
    search.mockRejectedValue(new Error('offline'));

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);

    expect(component.hasError()).toBe(true);
    expect(component.showNoResults()).toBe(false);
  });

  it('cancels a pending search when the field is cleared', async () => {
    vi.useFakeTimers();
    let requestSignal: AbortSignal | undefined;
    search.mockImplementation((_query, signal) => {
      requestSignal = signal;
      return new Promise<PlaceSearchResult[]>((resolve) => {
        signal.addEventListener('abort', () => resolve([]), { once: true });
      });
    });

    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'Alta' } }));
    await vi.advanceTimersByTimeAsync(250);
    component.onSearchClear();

    expect(search).toHaveBeenCalledOnce();
    expect(requestSignal?.aborted).toBe(true);
    expect(component.results()).toEqual([]);
    expect(component.showResults()).toBe(false);
  });

  it('emits the selected place, keeps its name, and closes the suggestions', () => {
    const selected: PlaceSearchResult[] = [];
    component.placeSelected.subscribe((place) => selected.push(place));
    component.results.set(places);
    component.showResults.set(true);
    component.searchTerm.set('Alta');

    component.onPlaceSelect(places[0]);

    expect(selected).toEqual([places[0]]);
    expect(component.results()).toEqual([]);
    expect(component.searchTerm()).toBe('Alta');
    expect(component.showResults()).toBe(false);
  });
});