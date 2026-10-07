import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationListComponent } from './observation-list.component';
import { ObservationListInfoDto } from '@shared/types/api.types';
import { buildObservationTree, ObservationGrouping, registrationStatus } from './observation-list.model';
import { LocaleDatePipe } from '@shared/pipes/locale-date.pipe';

const record = (id: number, overrides: Partial<ObservationListInfoDto> = {}): ObservationListInfoDto => ({
  id,
  taxonId: 1,
  preferredPopularName: 'Lav',
  scientificName: 'Lichen',
  displayName: 'Lav',
  taxonGroupId: 1,
  taxonGroupName: 'Lav',
  locationId: 5,
  locality: 'Åsen',
  categoryCode: 'VU',
  categoryTypeId: 1,
  dateTimeCollected: '2024-10-05',
  collector: 'Per',
  ...overrides,
});
const tree = (records: ObservationListInfoDto[], mode: ObservationGrouping = 'taxonGroup') =>
  buildObservationTree(
    records,
    mode,
    'no',
    (key) => key.split('.').at(-1)!,
    (date) => new LocaleDatePipe().transform(date, 'no'),
  );

describe('Observation grouping', () => {
  it('groups by IDs, preserves all records, and orders statuses and dates', () => {
    const result = tree([
      record(1, { registrationType: ['Absent', 'NotRecovered'] }),
      record(2, { registrationType: ['NotRecovered'] }),
      record(3, { dateTimeCollected: null }),
      record(4, { dateTimeCollected: '2025-01-01' }),
      record(5, { taxonId: 2 }),
      record(6, { taxonGroupId: 2 }),
    ]);
    expect(result).toHaveLength(2);
    expect(result.map((group) => group.count)).toEqual([5, 1]);
    expect(result[0].children.map((node) => node.status)).toEqual(['found', 'notRecovered', 'absent']);
    expect(result[0].children[0].children).toHaveLength(2);
    expect(result[0].children[0].children[0].children.map((node) => node.id.split('/').at(-1))).toEqual(['4', '3']);
    expect(registrationStatus(record(1, { registrationType: ['Other'] }))).toBe('found');
  });

  it('keeps equal-name localities separate', () => {
    expect(tree([record(1), record(2, { locationId: 6 })], 'location')).toHaveLength(2);
  });

  it.each(['redList', 'alienSpecies'] as const)('merges nonmatching records into the default category in %s', (mode) => {
    const fallback = mode === 'redList' ? 'NE' : 'NR';
    const type = mode === 'redList' ? 1 : 2;
    const records = [
      record(1),
      record(2, { categoryCode: 'SE', categoryTypeId: 2 }),
      record(3, { categoryCode: null, taxonId: 3 }),
      record(4, { categoryCode: fallback, categoryTypeId: type }),
      record(5, { categoryCode: 'unexpected', taxonId: 5 }),
      record(6, { categoryCode: 'EN', categoryTypeId: null }),
    ];
    const groups = tree(records, mode);
    expect(groups.map((group) => group.id)).toEqual([`${mode}/${mode === 'redList' ? 'VU' : 'SE'}`, `${mode}/${fallback}`]);
    expect(groups.reduce((sum, group) => sum + group.count, 0)).toBe(records.length);
    const defaultGroup = groups[1];
    expect(defaultGroup.count).toBe(5);
    expect(defaultGroup.label).toBe(fallback);
    expect(defaultGroup.badges.map((badge) => badge.code)).toEqual([fallback]);
    expect(defaultGroup.children[0].badges.map((badge) => badge.code)).toContain(mode === 'redList' ? 'SE' : 'VU');
    expect(defaultGroup.children[0].children.find((species) => species.id.endsWith('/3'))?.badges).toEqual([]);
    expect(records[1].categoryCode).toBe('SE');
    expect(records[2].categoryCode).toBeNull();
  });

  it('deduplicates badges and dots without discarding mixed-category records', () => {
    const groups = tree([record(1), record(2), record(3, { categoryCode: 'EN' })]);
    expect(groups[0].children[0].badges.map((badge) => badge.code)).toEqual(['EN', 'VU']);
    expect(groups[0].children[0].children[0].badges.map((badge) => badge.code)).toEqual(['EN', 'VU']);
    expect(groups[0].count).toBe(3);
  });

  it('handles scientific and unknown values without losing observations', () => {
    const species = tree([record(1, { displayName: '', preferredPopularName: null, collector: null, dateTimeCollected: 'invalid' })])[0]
      .children[0].children[0];
    expect(species.scientific).toBe(true);
    expect(species.label).toBe('Lichen');
    expect(species.children[0].label).toBe('unknownDate: unknownCollector');
    expect(tree([record(1, { categoryCode: 'unexpected' })], 'redList')[0].label).toBe('NE');
  });

  it.each(['taxonGroup', 'location', 'redList', 'alienSpecies'] as const)(
    'uses the backend display name for species under every registration status in %s',
    (mode) => {
      const groups = tree(
        [
          record(1, { displayName: ' Selected name ', preferredPopularName: null }),
          record(2, { displayName: 'Selected name', preferredPopularName: 'Other name', registrationType: ['NotRecovered'] }),
          record(3, { displayName: 'Selected name', registrationType: ['Absent'] }),
        ],
        mode,
      );
      expect(groups[0].children.map((status) => status.children[0].label)).toEqual(['Selected name', 'Selected name', 'Selected name']);
    },
  );

  it.each([
    { displayName: undefined, preferredPopularName: 'Popular name', scientificName: 'Scientific name', expected: 'Popular name' },
    { displayName: '  ', preferredPopularName: ' ', scientificName: 'Scientific name', expected: 'Scientific name' },
    { displayName: '', preferredPopularName: null, scientificName: null, expected: 'unknownSpecies' },
  ])('falls back when the display name is missing or blank: $expected', ({ expected, ...names }) => {
    expect(tree([record(1, names)])[0].children[0].children[0].label).toBe(expected);
  });

  it('retains every record in a 10,000-observation selection', () => {
    const groups = tree(Array.from({ length: 10000 }, (_, index) => record(index)));
    expect(groups[0].count).toBe(10000);
    expect(groups[0].children[0].children[0].children).toHaveLength(10000);
  });
});

describe('ObservationList', () => {
  let fixture: ComponentFixture<ObservationListComponent>;
  let component: ObservationListComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ObservationListComponent, TranslateModule.forRoot()] }).compileComponents();
    fixture = TestBed.createComponent(ObservationListComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('selection', { key: 1, kind: 'point', locationIds: [5], geometryLabel: 'UTM33 1, 2' });
    fixture.componentRef.setInput('observationList', [record(1)]);
    await fixture.whenStable();
  });

  it('renders the tree and changes grouping without fetching data', async () => {
    const root = fixture.nativeElement.querySelector('[role="tree"]');
    expect(fixture.nativeElement.querySelector('adb-accordion')).toBeNull();
    component.currentFilter.set('location');
    await fixture.whenStable();
    expect(component.groups()[0].label).toBe('Åsen');
    expect(fixture.nativeElement.querySelector('[role="tree"]')).toBe(root);
  });

  it('selects grouping through the dropdown', async () => {
    const dropdown: HTMLElement = fixture.nativeElement.querySelector('adb-dropdown');
    dropdown.dispatchEvent(new CustomEvent('adb-dropdown-select', { detail: { value: 'redList' } }));
    await fixture.whenStable();
    expect(component.currentFilter()).toBe('redList');
    const selected = [...fixture.nativeElement.querySelectorAll('adb-dropdown-item[selected]')];
    expect(selected.map((item) => item.getAttribute('value'))).toEqual(['redList']);
  });

  it('only exposes unambiguous header metadata', () => {
    fixture.componentRef.setInput('observationList', [record(1, { countyName: 'Trøndelag' }), record(2)]);
    expect(component.location()?.areas).toBe('');
    fixture.componentRef.setInput('selection', { key: 2, kind: 'selection', locationIds: [5, 6], geometryLabel: 'UTM33 1, 2' });
    expect(component.location()).toBeNull();
  });

  it('shows explicit loading and error states and emits retry', async () => {
    fixture.componentRef.setInput('requestState', 'loading');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="status"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[role="tree"]')).toBeNull();
    fixture.componentRef.setInput('requestState', 'error');
    await fixture.whenStable();
    const retry = vi.fn();
    component.retry.subscribe(retry);
    fixture.nativeElement.querySelector('button.retry').click();
    expect(retry).toHaveBeenCalledOnce();
  });

  it('emits dismiss from the close button', () => {
    const dismiss = vi.fn();
    component.dismiss.subscribe(dismiss);
    fixture.nativeElement.querySelector('.title-row adb-minimal-button').click();
    expect(dismiss).toHaveBeenCalledOnce();
  });

  it('updates dates when language changes', async () => {
    const translate = TestBed.inject(TranslateService);
    translate.use('en');
    await fixture.whenStable();
    expect(component.groups()[0].children[0].children[0].children[0].label).toContain('October');
    translate.use('no');
    await fixture.whenStable();
    expect(component.groups()[0].children[0].children[0].children[0].label).toContain('oktober');
  });
});
