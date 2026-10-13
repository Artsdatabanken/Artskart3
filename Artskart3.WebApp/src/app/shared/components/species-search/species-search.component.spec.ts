import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { SpeciesResultRow, SpeciesSearchComponent, getMatchLine, getVernacularName, toResultRow } from './species-search.component';
import { AutocompleteComponent } from '../autocomplete/autocomplete.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { SpeciesDto } from '../../types/api.types';

describe('SpeciesSearchComponent', () => {
  let component: SpeciesSearchComponent;
  let fixture: ComponentFixture<SpeciesSearchComponent>;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  const mockSpecies: SpeciesDto[] = [
    {
      taxonId: 1234,
      scientificName: 'Parus major',
      author: 'Linnaeus, 1758',
      preferredVernacularNames: [
        { name: 'Kjøttmeis', language: 'nb' },
        { name: 'Great Tit', language: 'en' },
      ],
    },
    {
      taxonId: 5678,
      scientificName: 'Parus caeruleus',
      author: 'Linnaeus, 1758',
      preferredVernacularNames: [{ name: 'Blåmeis', language: 'nb' }],
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SpeciesSearchComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(SpeciesSearchComponent);
    component = fixture.componentInstance;
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTesting.verify();
  });

  function showRows(term: string, species: SpeciesDto[]): HTMLElement {
    const autocomplete: AutocompleteComponent<SpeciesResultRow> = fixture.debugElement.query(
      By.directive(AutocompleteComponent),
    ).componentInstance;
    autocomplete.searchTerm.set(term);
    autocomplete.results.set(species.map(toResultRow));
    autocomplete.showAutocomplete.set(true);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('should add taxonId to FilterStateService on select', () => {
    component.onSelected(toResultRow(mockSpecies[0]));
    expect(filterState.selectedTaxonIds()).toContain(1234);
  });

  it('should not add to filter if taxonId is null', () => {
    component.onSelected(toResultRow({ taxonId: undefined, scientificName: 'Test' }));
    expect(filterState.selectedTaxonIds().length).toBe(0);
  });

  it('should search species and map the response to rows for the term', async () => {
    const rows = firstValueFrom(component.search('kjøtt'));
    httpTesting.expectOne((req) => req.url === '/api/Search/Species' && req.params.get('search') === 'kjøtt').flush(mockSpecies);
    expect((await rows).map((row) => row.vernacularName)).toEqual(['Kjøttmeis', 'Blåmeis']);
  });

  it('should add the taxon when an option is clicked', () => {
    const element = showRows('meis', mockSpecies);
    element.querySelectorAll<HTMLElement>('.autocomplete-item')[1].click();
    expect(filterState.selectedTaxonIds()).toEqual([5678]);
  });

  it('should render matches in the vernacular name as mark elements', () => {
    const vernacular = showRows('kjøtt', [mockSpecies[0]]).querySelector('.autocomplete-vernacular');
    expect(vernacular?.querySelector('mark')?.textContent).toBe('Kjøtt');
    expect(vernacular?.textContent).toBe('Kjøttmeis');
  });

  it('should render the formatted scientific name with italics and marks', () => {
    const scientific = showRows('pubescens', [
      {
        taxonId: 1,
        scientificName: 'Betula pubescens subsp. pubescens',
        scientificNameFormatted: '<i>Betula pubescens </i>subsp.<i> pubescens</i>',
      },
    ]).querySelector('.autocomplete-scientific');
    expect(scientific?.textContent).toBe('Betula pubescens subsp. pubescens');
    expect(scientific?.querySelectorAll('i mark').length).toBe(2);
  });

  describe('getVernacularName', () => {
    it('should return nb language name when available', () => {
      expect(getVernacularName(mockSpecies[0])).toBe('Kjøttmeis');
    });

    it('should return first name when nb is not available', () => {
      const species = {
        taxonId: 1,
        scientificName: 'Test',
        preferredVernacularNames: [{ name: 'English Name', language: 'en' }],
      };
      expect(getVernacularName(species)).toBe('English Name');
    });

    it('should return empty string when no vernacular names', () => {
      expect(getVernacularName({ taxonId: 1, scientificName: 'Test', preferredVernacularNames: [] })).toBe('');
      expect(getVernacularName({ taxonId: 1, scientificName: 'Test', preferredVernacularNames: undefined })).toBe('');
    });
  });

  describe('result rows', () => {
    const coryphellaSynonym: SpeciesDto = {
      taxonId: 100,
      scientificName: 'Fjordia chriskaugei',
      author: 'Padula, 2014',
      rank: 'Species',
      taxonGroupName: 'Bløtdyr',
      preferredVernacularNames: [{ name: 'Flanellsnegl', language: 'nb' }],
      vernacularNameSynonyms: [{ name: 'Lodden flanellsnegl', language: 'nb' }],
      scientificNameSynonyms: [{ name: 'Coryphella chriskaugei', nameFormatted: '<i>Coryphella chriskaugei</i>' }],
    };

    const matchLineFor = (term: string, species: SpeciesDto = coryphellaSynonym) => getMatchLine(toResultRow(species), term);

    it('should show no match line when the name itself matches', () => {
      expect(matchLineFor('fjordia chris')).toBeNull();
      expect(matchLineFor('flanell')).toBeNull();
    });

    it('should show the formatted scientific synonym when only it matches', () => {
      expect(matchLineFor('coryphella chris')).toEqual({
        kind: 'synonym',
        text: '<i>Coryphella chriskaugei</i>',
        isMarkup: true,
      });
    });

    it('should fall back to the plain scientific synonym when it has no formatted name', () => {
      const species = { ...coryphellaSynonym, scientificNameSynonyms: [{ name: 'Coryphella chriskaugei' }] };
      expect(matchLineFor('coryphella chris', species)?.text).toBe('Coryphella chriskaugei');
    });

    it('should show a vernacular synonym when only it matches', () => {
      expect(matchLineFor('lodden')).toEqual({ kind: 'synonym', text: 'Lodden flanellsnegl', isMarkup: false });
    });

    it('should show the author when it matches', () => {
      expect(matchLineFor('padula')).toEqual({ kind: 'author', text: 'Padula, 2014', isMarkup: false });
    });

    it('should show the taxon id for a numeric search', () => {
      expect(matchLineFor('100')).toEqual({ kind: 'id', text: '100', isMarkup: false });
    });

    it('should fall back to the plain scientific name when it has no formatted name', () => {
      expect(toResultRow(coryphellaSynonym).scientificNameMarkup).toBe('Fjordia chriskaugei');
      expect(toResultRow({ ...coryphellaSynonym, scientificNameFormatted: '<i>Fjordia chriskaugei</i>' }).scientificNameMarkup).toBe(
        '<i>Fjordia chriskaugei</i>',
      );
    });

    it('should expose the vernacular name and taxon group', () => {
      const row = toResultRow(coryphellaSynonym);
      expect(row.vernacularName).toBe('Flanellsnegl');
      expect(row.species.taxonGroupName).toBe('Bløtdyr');
    });

    it('should update the match line as the term changes, like the highlighting', () => {
      const element = showRows('coryphella', [coryphellaSynonym]);
      expect(element.querySelector('.autocomplete-match')?.textContent).toContain('Coryphella chriskaugei');

      const autocomplete: AutocompleteComponent<SpeciesResultRow> = fixture.debugElement.query(
        By.directive(AutocompleteComponent),
      ).componentInstance;
      autocomplete.searchTerm.set('flanell');
      fixture.detectChanges();

      expect(element.querySelector('.autocomplete-match')).toBeNull();
    });
  });
});
