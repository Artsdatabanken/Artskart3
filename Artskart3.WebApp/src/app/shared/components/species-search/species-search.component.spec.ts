import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { SpeciesSearchComponent } from './species-search.component';
import { SpeciesSearchService } from '../../services/species-search/species-search.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { SpeciesDto } from '../../types/api.types';

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
    component.showAutocomplete.set(true);
    component.onSearchSubmit();
    expect(filterState.selectedTaxonIds()).toContain(1234);
  });

  it('should do nothing on submit when no results', () => {
    component.speciesResults.set([]);
    component.onSearchSubmit();
    expect(filterState.selectedTaxonIds().length).toBe(0);
  });

  it('should fire a new request when retyping the same term after a selection', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(SpeciesSearchService);
      const searchSpy = vi.spyOn(service, 'searchSpecies').mockReturnValue(of(mockSpecies));
      const input = (value: string) =>
        component.onSearchInput(new CustomEvent('adb-input', { detail: { value } }) as unknown as Event);

      input('kj');
      vi.advanceTimersByTime(300);
      expect(searchSpy).toHaveBeenCalledTimes(1);

      component.onSpeciesSelect(mockSpecies[0]);
      input('kj');
      vi.advanceTimersByTime(300);
      expect(searchSpy).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should not reopen the popup when an in-flight response lands after Escape', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(SpeciesSearchService);
      const pending = new Subject<SpeciesDto[]>();
      vi.spyOn(service, 'searchSpecies').mockReturnValue(pending.asObservable());
      component.speciesResults.set(mockSpecies);
      component.showAutocomplete.set(true);

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'kjø' } }) as unknown as Event);
      vi.advanceTimersByTime(300);
      component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
      expect(component.showAutocomplete()).toBe(false);

      pending.next(mockSpecies);
      expect(component.showAutocomplete()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should cancel an in-flight request and stay closed when cleared', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(SpeciesSearchService);
      const pending = new Subject<SpeciesDto[]>();
      vi.spyOn(service, 'searchSpecies').mockReturnValue(pending.asObservable());

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'kjø' } }) as unknown as Event);
      vi.advanceTimersByTime(300);
      component.onSearchClear();
      vi.advanceTimersByTime(0); // only the 0 ms path can have fired
      expect(pending.observed).toBe(false); // inner subscription torn down
      pending.next(mockSpecies); // late response is a no-op
      expect(component.showAutocomplete()).toBe(false);
      expect(component.speciesResults().length).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should keep dismissed results fresh for an ArrowDown reopen', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(SpeciesSearchService);
      const pending = new Subject<SpeciesDto[]>();
      vi.spyOn(service, 'searchSpecies').mockReturnValue(pending.asObservable());

      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'kjø' } }) as unknown as Event);
      vi.advanceTimersByTime(300);
      component.speciesResults.set(mockSpecies);
      component.showAutocomplete.set(true);
      component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));

      const fresh = [{ ...mockSpecies[0], taxonId: 9999 }];
      pending.next(fresh);
      expect(component.showAutocomplete()).toBe(false);
      expect(component.speciesResults()[0].taxonId).toBe(9999);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should restore focus to the first option when a response replaces the list', () => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(SpeciesSearchService);
      const first = new Subject<SpeciesDto[]>();
      const second = new Subject<SpeciesDto[]>();
      const searchSpy = vi.spyOn(service, 'searchSpecies');
      searchSpy.mockReturnValueOnce(first.asObservable()).mockReturnValueOnce(second.asObservable());
      const input = (value: string) =>
        component.onSearchInput(new CustomEvent('adb-input', { detail: { value } }) as unknown as Event);

      input('kj');
      vi.advanceTimersByTime(300);
      first.next(mockSpecies);
      fixture.detectChanges();
      fixture.nativeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', cancelable: true, bubbles: true }));
      fixture.detectChanges();
      expect(fixture.nativeElement.ownerDocument.activeElement?.id).toBe('species-option-0');

      input('kjø');
      vi.advanceTimersByTime(300);
      second.next([{ ...mockSpecies[1], taxonId: 9999 }]);
      fixture.detectChanges();

      expect(component.highlightedIndex()).toBe(0);
      expect(fixture.nativeElement.ownerDocument.activeElement?.id).toBe('species-option-0');
    } finally {
      vi.useRealTimers();
    }
  });

  describe('keyboard navigation', () => {
    // Dispatch real bubbling events so the host: { '(keydown)': ... } binding is
    // actually exercised — calling onKeydown() directly would pass even if the
    // binding were deleted.
    const keydown = (key: string, target?: HTMLElement) => {
      const event = new KeyboardEvent('keydown', { key, cancelable: true, bubbles: true });
      (target ?? fixture.nativeElement).dispatchEvent(event);
      return event;
    };

    beforeEach(() => {
      component.speciesResults.set(mockSpecies);
      component.showAutocomplete.set(true);
    });

    it('should handle arrow keys bubbling up from a focused option', () => {
      fixture.detectChanges();
      component.highlightedIndex.set(0);
      const option = fixture.nativeElement.querySelector('#species-option-0') as HTMLElement;
      keydown('ArrowDown', option);
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should move highlight down on ArrowDown', () => {
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(0);
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should wrap to first item when pressing ArrowDown on the last item', () => {
      keydown('ArrowDown');
      keydown('ArrowDown');
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(0);
    });

    it('should wrap to last item when pressing ArrowUp with nothing highlighted', () => {
      keydown('ArrowUp');
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should prevent default on arrow keys', () => {
      expect(keydown('ArrowDown').defaultPrevented).toBe(true);
      expect(keydown('ArrowUp').defaultPrevented).toBe(true);
    });

    it('should ignore arrow keys when there are no results', () => {
      component.showAutocomplete.set(false);
      component.speciesResults.set([]);
      const event = keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(-1);
      expect(component.showAutocomplete()).toBe(false);
      expect(event.defaultPrevented).toBe(false);
    });

    it('should select the highlighted item on submit', () => {
      keydown('ArrowDown');
      keydown('ArrowDown');
      component.onSearchSubmit();
      expect(filterState.selectedTaxonIds()).toContain(5678);
    });

    it('should fall back to the first result on submit when nothing is highlighted', () => {
      component.onSearchSubmit();
      expect(filterState.selectedTaxonIds()).toContain(1234);
    });

    it('should close the dropdown and reset the highlight on Escape', () => {
      keydown('ArrowDown');
      component.onSearchClear();
      component.speciesResults.set(mockSpecies);
      component.showAutocomplete.set(true);
      keydown('ArrowDown');
      const event = keydown('Escape');
      expect(event.defaultPrevented).toBe(true);
      expect(component.showAutocomplete()).toBe(false);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should not select anything on submit after Escape dismissed the popup', () => {
      keydown('Escape');
      component.onSearchSubmit();
      expect(filterState.selectedTaxonIds().length).toBe(0);
    });

    it('should reopen the popup on ArrowDown after Escape dismissed it', () => {
      fixture.detectChanges();
      keydown('Escape');
      fixture.detectChanges();
      // The list must really be gone from the DOM — focus must survive that.
      expect(fixture.nativeElement.querySelector('.autocomplete-list')).toBeNull();
      keydown('ArrowDown');
      expect(component.showAutocomplete()).toBe(true);
      expect(component.highlightedIndex()).toBe(0);
      fixture.detectChanges();
      expect(fixture.nativeElement.ownerDocument.activeElement?.id).toBe('species-option-0');
    });

    it('should keep the live region in the DOM even when the popup is closed', () => {
      component.showAutocomplete.set(false);
      component.showNoResults.set(false);
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[role="status"]')).toBeTruthy();
    });

    it('should announce the result count in the live region when open', () => {
      fixture.detectChanges();
      const liveRegion = fixture.nativeElement.querySelector('[role="status"]');
      expect(liveRegion).toBeTruthy();
      expect(liveRegion.textContent.trim().length).toBeGreaterThan(0);
    });

    it('should reset the highlight on new input', () => {
      keydown('ArrowDown');
      component.onSearchInput(new CustomEvent('adb-input', { detail: { value: 'kjø' } }) as unknown as Event);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should reset the highlight after selecting a species', () => {
      keydown('ArrowDown');
      component.onSpeciesSelect(mockSpecies[0]);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should rove tabindex so only the highlighted option is tabbable', () => {
      fixture.detectChanges();
      keydown('ArrowDown');
      fixture.detectChanges();
      const options = fixture.nativeElement.querySelectorAll('.autocomplete-item');
      expect(options[0].getAttribute('tabindex')).toBe('0');
      expect(options[1].getAttribute('tabindex')).toBe('-1');
    });

    it('should move DOM focus to the highlighted option', () => {
      fixture.detectChanges();
      keydown('ArrowDown');
      fixture.detectChanges();
      expect(fixture.nativeElement.ownerDocument.activeElement?.id).toBe('species-option-0');
    });

    it('should select the species on Enter keydown on an option', () => {
      fixture.detectChanges();
      const option = fixture.nativeElement.querySelector('#species-option-1');
      option.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      fixture.detectChanges();
      expect(filterState.selectedTaxonIds()).toContain(5678);
    });

    it('should select the species on Space keydown on an option', () => {
      fixture.detectChanges();
      const option = fixture.nativeElement.querySelector('#species-option-1');
      const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });
      option.dispatchEvent(event);
      fixture.detectChanges();
      expect(filterState.selectedTaxonIds()).toContain(5678);
      expect(event.defaultPrevented).toBe(true);
    });
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
