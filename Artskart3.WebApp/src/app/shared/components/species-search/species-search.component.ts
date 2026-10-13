import { Component, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { map } from 'rxjs';
import { SpeciesSearchService } from '../../services/species-search/species-search.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { SpeciesDto } from '../../types/api.types';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';
import { AutocompleteOptionDirective, AutocompleteSearch } from '../autocomplete/autocomplete-option.directive';
import { HighlightTextComponent } from '../highlight-text/highlight-text.component';

export interface SpeciesMatchLine {
  kind: 'id' | 'author' | 'synonym';
  text: string;
  isMarkup: boolean;
}

export interface SpeciesResultRow {
  species: SpeciesDto;
  vernacularName: string;
  scientificNameMarkup: string;
}

@Component({
  selector: 'app-species-search',
  imports: [TranslateModule, AutocompleteComponent, AutocompleteOptionDirective, HighlightTextComponent],
  templateUrl: './species-search.component.html',
  styleUrl: './species-search.component.css',
})
export class SpeciesSearchComponent {
  private readonly speciesSearchService = inject(SpeciesSearchService);
  private readonly filterState = inject(FilterStateService);

  readonly search: AutocompleteSearch<SpeciesResultRow> = (term) =>
    this.speciesSearchService.searchSpecies(term).pipe(map((list) => list.map(toResultRow)));
  readonly trackByTaxonId = (row: SpeciesResultRow) => row.species.taxonId;
  // Follows the term as it is typed, like the highlighting, not the term the results came from.
  protected readonly matchLine = getMatchLine;

  onSelected(row: SpeciesResultRow): void {
    if (row.species.taxonId == null) return;
    this.filterState.addTaxon(row.species.taxonId);
  }
}

export function toResultRow(species: SpeciesDto): SpeciesResultRow {
  return {
    species,
    vernacularName: getVernacularName(species),
    scientificNameMarkup: species.scientificNameFormatted || species.scientificName || '',
  };
}

export function getVernacularName(species: SpeciesDto): string {
  const names = species.preferredVernacularNames;
  if (!names || names.length === 0) return '';
  const nbName = names.find((n) => n.language === 'nb');
  return (nbName ?? names[0])?.name ?? '';
}

export function getMatchLine({ species, vernacularName }: SpeciesResultRow, term: string): SpeciesMatchLine | null {
  if (/^\d+$/.test(term)) {
    return species.taxonId == null ? null : { kind: 'id', text: String(species.taxonId), isMarkup: false };
  }
  const words = term.split(/\s+/).filter((w) => w.length > 0);
  const matches = (text: string | null | undefined) => matchesSearch(text, words);
  if (!matches(vernacularName) && !matches(species.scientificName)) {
    const scientificSynonym = species.scientificNameSynonyms?.find((s) => matches(s.name));
    if (scientificSynonym?.name) {
      return { kind: 'synonym', text: scientificSynonym.nameFormatted || scientificSynonym.name, isMarkup: true };
    }
    const vernacularSynonym = species.vernacularNameSynonyms?.find((s) => matches(s.name));
    if (vernacularSynonym?.name) return { kind: 'synonym', text: vernacularSynonym.name, isMarkup: false };
  }
  if (species.author && matches(species.author)) {
    return { kind: 'author', text: species.author, isMarkup: false };
  }
  return null;
}

function matchesSearch(text: string | null | undefined, words: string[]): boolean {
  if (!text || words.length === 0) return false;
  const lower = text.toLowerCase();
  return words.every((w) => lower.includes(w.toLowerCase()));
}
