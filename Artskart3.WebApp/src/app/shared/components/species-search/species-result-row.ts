import { Pipe, PipeTransform } from '@angular/core';
import { SpeciesDto } from '../../types/api.types';

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

/** The match line for a row, following the term as it is typed, like the highlighting. */
@Pipe({ name: 'speciesMatchLine' })
export class SpeciesMatchLinePipe implements PipeTransform {
  transform(row: SpeciesResultRow, term: string): SpeciesMatchLine | null {
    return getMatchLine(row, term);
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
