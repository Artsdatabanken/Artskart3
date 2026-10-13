import { Component, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { map } from 'rxjs';
import { SpeciesSearchService } from '../../services/species-search/species-search.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';
import { AutocompleteOptionDirective, AutocompleteSearch } from '../autocomplete/autocomplete-option.directive';
import { HighlightTextComponent } from '../highlight-text/highlight-text.component';
import { SpeciesMatchLinePipe, SpeciesResultRow, toResultRow } from './species-result-row';

@Component({
  selector: 'app-species-search',
  imports: [TranslateModule, AutocompleteComponent, AutocompleteOptionDirective, HighlightTextComponent, SpeciesMatchLinePipe],
  templateUrl: './species-search.component.html',
  styleUrl: './species-search.component.css',
})
export class SpeciesSearchComponent {
  private readonly speciesSearchService = inject(SpeciesSearchService);
  private readonly filterState = inject(FilterStateService);

  readonly search: AutocompleteSearch<SpeciesResultRow> = (term) =>
    this.speciesSearchService.searchSpecies(term).pipe(map((list) => list.map(toResultRow)));
  readonly trackByTaxonId = (row: SpeciesResultRow) => row.species.taxonId;

  onSelected(row: SpeciesResultRow): void {
    if (row.species.taxonId == null) return;
    this.filterState.addTaxon(row.species.taxonId);
  }
}
