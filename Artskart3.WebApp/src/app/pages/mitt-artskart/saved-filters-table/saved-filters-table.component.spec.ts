import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, of } from 'rxjs';
import { SavedFiltersTableComponent } from './saved-filters-table.component';
import { SavedFilterService } from '../../../shared/services/saved-filter/saved-filter.service';
import { SavedFilterDto } from '../../../shared/types/api.types';

describe('SavedFiltersTableComponent', () => {
  let fixture: ComponentFixture<SavedFiltersTableComponent>;

  const FILTERS: SavedFilterDto[] = [
    { id: 'a', name: 'Nakensnegler i Trondheim', filter: {}, isDefault: false, createdAt: '2025-01-13T13:49:00Z' },
    { id: 'b', name: 'Pattedyr i Trøndelag', filter: {}, isDefault: true, createdAt: '2025-01-13T08:39:00Z' },
  ];

  const value = signal<SavedFilterDto[]>(FILTERS);
  const savedFilterService = {
    filtersResource: {
      value,
      hasValue: () => true,
      isLoading: () => false,
      error: () => undefined,
    },
    delete: vi.fn(() => of(undefined)),
    setDefault: vi.fn(() => of(undefined)),
    clearDefault: vi.fn(() => of(undefined)),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    value.set(FILTERS);

    await TestBed.configureTestingModule({
      imports: [SavedFiltersTableComponent, TranslateModule.forRoot()],
      providers: [provideRouter([]), { provide: SavedFilterService, useValue: savedFilterService }],
    }).compileComponents();

    fixture = TestBed.createComponent(SavedFiltersTableComponent);
    fixture.detectChanges();
  });

  const el = (): HTMLElement => fixture.nativeElement;
  const rows = (): HTMLElement[] => Array.from(el().querySelectorAll('tbody tr'));

  it('viser en rad per filter med standard-merket på standardfilteret', () => {
    expect(rows()).toHaveLength(2);
    expect(rows()[0].querySelector('.default-tag')).toBeNull();
    expect(rows()[1].querySelector('.default-tag')?.textContent?.trim()).toBe('savedFilters.defaultTag');
  });

  it('lenker til kartet med filter-ID-en', () => {
    const link = rows()[0].querySelector('a[href^="/"]') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/?filter=a');
  });

  it('viser en tom-melding uten filtre', () => {
    value.set([]);
    fixture.detectChanges();

    expect(el().querySelector('.empty-message')?.textContent?.trim()).toBe('savedFilters.noFilters');
  });

  it('sletter først når brukeren bekrefter', () => {
    (rows()[0].querySelector('adb-icon-button') as HTMLElement).click();
    fixture.detectChanges();

    expect(el().querySelector('[role="dialog"]')).toBeTruthy();
    expect(savedFilterService.delete).not.toHaveBeenCalled();

    fixture.componentInstance.onConfirmDelete();
    fixture.detectChanges();

    expect(savedFilterService.delete).toHaveBeenCalledWith('a');
    expect(el().querySelector('[role="dialog"]')).toBeNull();
  });

  it('setter og fjerner standardfilter med en knapp', () => {
    const buttons = rows().map((row) => row.querySelector('button.link-button') as HTMLButtonElement);
    buttons[0].click();
    buttons[1].click();

    expect(savedFilterService.setDefault).toHaveBeenCalledWith('a');
    expect(savedFilterService.clearDefault).toHaveBeenCalledWith('b');
  });

  it('sender ikke en ny forespørsel mens den forrige pågår', () => {
    const pending = new Subject<undefined>();
    savedFilterService.setDefault.mockReturnValueOnce(pending);

    fixture.componentInstance.onToggleDefault(FILTERS[0]);
    fixture.componentInstance.onToggleDefault(FILTERS[0]);

    expect(savedFilterService.setDefault).toHaveBeenCalledTimes(1);
    fixture.detectChanges();
    const buttons = rows().map((row) => row.querySelector('button.link-button') as HTMLButtonElement);
    expect(buttons.every((button) => button.disabled)).toBe(true);
    pending.complete();
    expect(fixture.componentInstance.updatingId()).toBeNull();
  });
});
