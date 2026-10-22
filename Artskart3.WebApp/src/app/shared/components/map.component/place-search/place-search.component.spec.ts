import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import type { PlaceSearchResult } from '@artsdatabanken/nbic-map-component';
import { AutocompleteComponent } from '../../autocomplete/autocomplete.component';
import type { AutocompleteSearch } from '../../autocomplete/autocomplete-option.directive';
import { PlaceSearchComponent } from './place-search.component';

describe('PlaceSearchComponent', () => {
  let fixture: ComponentFixture<PlaceSearchComponent>;
  let component: PlaceSearchComponent;
  let autocomplete: AutocompleteComponent<PlaceSearchResult>;

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
    const search: AutocompleteSearch<PlaceSearchResult> = () => of(places);
    fixture.componentRef.setInput('search', search);
    fixture.detectChanges();
    autocomplete = fixture.debugElement.query(By.directive(AutocompleteComponent)).componentInstance;
  });

  it('renders place context and type and forwards the selected result', () => {
    const selected: PlaceSearchResult[] = [];
    component.selected.subscribe((place) => selected.push(place));
    autocomplete.results.set(places);
    autocomplete.showAutocomplete.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.place-row')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Kvænangen, Troms');
    expect(fixture.nativeElement.textContent).toContain('Fjord');

    autocomplete.select(places[0]);

    expect(selected).toEqual([places[0]]);
  });
});