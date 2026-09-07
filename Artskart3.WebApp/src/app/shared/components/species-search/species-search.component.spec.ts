import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { SpeciesSearchComponent } from './species-search.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

describe('SpeciesSearchComponent', () => {
  let component: SpeciesSearchComponent;
  let fixture: ComponentFixture<SpeciesSearchComponent>;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  const mockSpecies = [
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

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should hide autocomplete when input is less than 2 characters', () => {
    component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'k' } }) as unknown as Event);
    expect(component.showAutocomplete()).toBe(false);
    expect(component.speciesResults().length).toBe(0);
  });

  it('should hide autocomplete and clear results on clear', () => {
    component.speciesResults.set(mockSpecies);
    component.showAutocomplete.set(true);
    component.searchTerm.set('kj');

    component.onSearchClear();

    expect(component.showAutocomplete()).toBe(false);
    expect(component.speciesResults().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should add taxonId to FilterStateService on species select', () => {
    component.onSpeciesSelect(mockSpecies[0]);
    expect(filterState.selectedTaxonIds()).toContain(1234);
  });

  it('should clear search state after selecting a species', () => {
    component.speciesResults.set(mockSpecies);
    component.showAutocomplete.set(true);
    component.searchTerm.set('kj');

    component.onSpeciesSelect(mockSpecies[0]);

    expect(component.showAutocomplete()).toBe(false);
    expect(component.speciesResults().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should not add to filter if taxonId is null', () => {
    component.onSpeciesSelect({ taxonId: undefined, scientificName: 'Test' });
    expect(filterState.selectedTaxonIds().length).toBe(0);
  });

  it('should select first result on search submit', () => {
    component.speciesResults.set(mockSpecies);
    component.onSearchSubmit();
    expect(filterState.selectedTaxonIds()).toContain(1234);
  });

  it('should do nothing on submit when no results', () => {
    component.speciesResults.set([]);
    component.onSearchSubmit();
    expect(filterState.selectedTaxonIds().length).toBe(0);
  });

  describe('getVernacularName', () => {
    it('should return nb language name when available', () => {
      expect(component.getVernacularName(mockSpecies[0])).toBe('Kjøttmeis');
    });

    it('should return first name when nb is not available', () => {
      const species = {
        taxonId: 1,
        scientificName: 'Test',
        preferredVernacularNames: [{ name: 'English Name', language: 'en' }],
      };
      expect(component.getVernacularName(species)).toBe('English Name');
    });

    it('should return empty string when no vernacular names', () => {
      expect(component.getVernacularName({ taxonId: 1, scientificName: 'Test', preferredVernacularNames: [] })).toBe('');
      expect(component.getVernacularName({ taxonId: 1, scientificName: 'Test', preferredVernacularNames: null })).toBe('');
    });
  });

  describe('highlightMatch', () => {
    it('should bold matching substring', () => {
      component.searchTerm.set('kjøtt');
      expect(component.highlightMatch('Kjøttmeis')).toBe('<strong>Kjøtt</strong>meis');
    });

    it('should highlight multiple words independently', () => {
      component.searchTerm.set('kj m');
      const result = component.highlightMatch('Kjøttmeis');
      expect(result).toBe('<strong>Kj</strong>øtt<strong>m</strong>eis');
    });

    it('should return original text when no search term', () => {
      component.searchTerm.set('');
      expect(component.highlightMatch('Kjøttmeis')).toBe('Kjøttmeis');
    });

    it('should return empty string for null/undefined input', () => {
      component.searchTerm.set('test');
      expect(component.highlightMatch(null)).toBe('');
      expect(component.highlightMatch(undefined)).toBe('');
    });

    it('should escape regex special characters in search term', () => {
      component.searchTerm.set('test(1)');
      expect(component.highlightMatch('this is test(1) here')).toBe('this is <strong>test(1)</strong> here');
    });
  });
});
