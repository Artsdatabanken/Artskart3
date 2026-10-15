import { Component, input, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import type { PlaceSearchResult } from '@artsdatabanken/nbic-map-component';
import { AutocompleteComponent } from '../../autocomplete/autocomplete.component';
import { AutocompleteOptionDirective, AutocompleteSearch } from '../../autocomplete/autocomplete-option.directive';

@Component({
  selector: 'app-place-search',
  imports: [TranslateModule, AutocompleteComponent, AutocompleteOptionDirective],
  templateUrl: './place-search.component.html',
  styleUrl: './place-search.component.css',
})
export class PlaceSearchComponent {
  readonly search = input.required<AutocompleteSearch<PlaceSearchResult>>();
  readonly selected = output<PlaceSearchResult>();
  readonly trackById = (place: PlaceSearchResult) => place.id;

  locationLabel(place: PlaceSearchResult): string {
    return [...place.municipalities, ...place.counties].join(', ');
  }
}