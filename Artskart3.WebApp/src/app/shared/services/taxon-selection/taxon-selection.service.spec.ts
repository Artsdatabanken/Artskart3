import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TaxonSelectionService } from './taxon-selection.service';
import { FilterStateService } from '../filter-state/filter-state.service';
import { TaxonTreeNodeDto } from '../../types/api.types';

function node(id: number, extra: Partial<TaxonTreeNodeDto> = {}): TaxonTreeNodeDto {
  return {
    id,
    validScientificName: `Taxon ${id}`,
    taxonRankId: 10,
    taxonGroupId: 1,
    existsInCountry: true,
    hasChildren: false,
    children: [],
    ...extra,
  };
}

describe('TaxonSelectionService', () => {
  let service: TaxonSelectionService;
  let filterState: FilterStateService;
  let httpTesting: HttpTestingController;

  // Hierarki: 1 -> {2, 3}; 2 -> {4, 5}
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TaxonSelectionService);
    filterState = TestBed.inject(FilterStateService);
    httpTesting = TestBed.inject(HttpTestingController);

    service.registerChildren(undefined, [node(1), node(6)]);
    service.registerChildren(1, [node(2), node(3)]);
    service.registerChildren(2, [node(4), node(5)]);
    service.registerAncestry(1, []);
    service.registerAncestry(2, [1]);
    service.registerAncestry(3, [1]);
    service.registerAncestry(4, [1, 2]);
    service.registerAncestry(5, [1, 2]);
    service.registerAncestry(6, []);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('unchecked når ingenting er valgt', () => {
    expect(service.checkboxState(4, [1, 2])).toBe('unchecked');
  });

  it('checked når noden selv er valgt', () => {
    filterState.setTaxons([4]);
    expect(service.checkboxState(4, [1, 2])).toBe('checked');
  });

  it('checked når en forgjenger er valgt (arvet)', () => {
    filterState.setTaxons([1]);
    expect(service.checkboxState(4, [1, 2])).toBe('checked');
  });

  it('indeterminate når en etterkommer er valgt', () => {
    filterState.setTaxons([4]);
    expect(service.checkboxState(2, [1])).toBe('indeterminate');
    expect(service.checkboxState(1, [])).toBe('indeterminate');
    expect(service.checkboxState(3, [1])).toBe('unchecked');
  });

  it('select legger til id og fjerner kjente etterkommere fra utvalget', () => {
    filterState.setTaxons([4, 5]);
    service.select(2);
    expect(filterState.selectedTaxonIds()).toEqual([2]);
  });

  it('select erstatter ikke barna med forelderen når alle barna er valgt', () => {
    filterState.setTaxons([4]);
    service.select(5);
    expect(filterState.selectedTaxonIds()).toEqual([4, 5]);
    expect(service.checkboxState(2, [1])).toBe('indeterminate');
  });

  it('select av eneste barn velger barnet, ikke forelderen', () => {
    // F.eks. en art med én underart: underarten dekker ikke observasjonene bestemt til art
    service.registerChildren(3, [node(7)]);
    service.registerAncestry(7, [1, 3]);

    service.select(7);
    expect(filterState.selectedTaxonIds()).toEqual([7]);
    expect(service.checkboxState(7, [1, 3])).toBe('checked');
    expect(service.checkboxState(3, [1])).toBe('indeterminate');
  });

  it('deselect av eksplisitt valgt node fjerner id-en', async () => {
    filterState.setTaxons([4, 6]);
    await service.deselect(4, [1, 2]);
    expect(filterState.selectedTaxonIds()).toEqual([6]);
  });

  it('deselect av arvet checked node materialiserer søsknene', async () => {
    filterState.setTaxons([1]);
    await service.deselect(4, [1, 2]);
    // 1 erstattes av søsknene langs stien: 3 (søsken av 2) og 5 (søsken av 4)
    expect(filterState.selectedTaxonIds()).toEqual([3, 5]);
  });

  it('re-check av materialisert node legger bare til noden', async () => {
    filterState.setTaxons([1]);
    await service.deselect(4, [1, 2]);
    expect(filterState.selectedTaxonIds()).toEqual([3, 5]);

    service.select(4);
    expect(filterState.selectedTaxonIds()).toEqual([3, 5, 4]);
    expect(service.checkboxState(1, [])).toBe('indeterminate');
  });

  it('deselect overskriver ikke et samtidig select', async () => {
    filterState.setTaxons([1]);

    const deselecting = service.deselect(4, [1, 2]);
    // Brukeren rekker å velge en annen node mens materialiseringen pågår
    service.select(6);
    await deselecting;

    expect(filterState.selectedTaxonIds()).toContain(6);
    expect(filterState.selectedTaxonIds()).toContain(3);
    expect(filterState.selectedTaxonIds()).toContain(5);
    expect(filterState.selectedTaxonIds()).not.toContain(1);
  });

  it('deselect henter manglende barnenivå fra API', async () => {
    // 6 er ikke utforsket i treet: barna er ikke i cachen
    filterState.setTaxons([6]);
    service.registerAncestry(7, [6]);

    const promise = service.deselect(7, [6]);
    const req = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonTree' && r.params.get('parentTaxonId') === '6');
    req.flush([node(7), node(8)]);
    await promise;

    expect(filterState.selectedTaxonIds()).toEqual([8]);
  });

  it('resolveAncestries henter kjeder for ukjente valgte id-er i én batch', () => {
    filterState.setTaxons([4, 42]);
    service.resolveAncestries();

    // 4 er allerede kjent — bare 42 hentes
    const req = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    expect(req.request.params.getAll('taxonIds')).toEqual(['42']);
    req.flush([{ id: 42, parentIds: [1, 2] }]);

    expect(service.isIndeterminate(2)).toBe(true);
    expect(service.checkboxState(2, [1])).toBe('indeterminate');
  });

  it('henter automatisk kjeder når et ukjent taxon velges utenfor treet', async () => {
    // Simulerer valg via artsøk — ingen manuell resolveAncestries-invokering
    filterState.addTaxon(42);
    TestBed.tick();

    const req = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    expect(req.request.params.getAll('taxonIds')).toEqual(['42']);
    req.flush([{ id: 42, parentIds: [1, 2] }]);
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    expect(service.isIndeterminate(2)).toBe(true);
  });

  it('plukker opp id-er som velges mens en ancestry-request pågår', async () => {
    filterState.addTaxon(42);
    TestBed.tick();
    const first = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');

    // Nytt valg mens første request pågår
    filterState.addTaxon(43);
    TestBed.tick();
    httpTesting.expectNone((r) => r.url === '/api/Lookup/TaxonAncestry' && r.params.getAll('taxonIds')!.includes('43'));

    first.flush([{ id: 42, parentIds: [1] }]);
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    const second = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    expect(second.request.params.getAll('taxonIds')).toEqual(['43']);
    second.flush([{ id: 43, parentIds: [1] }]);
  });

  it('forelder til eneste valgte barn fra artsøk er indeterminate, ikke checked', async () => {
    // 42 er eneste barn av 41
    service.registerChildren(41, [node(42)]);
    filterState.addTaxon(42);
    TestBed.tick();

    const req = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    req.flush([{ id: 42, parentIds: [1, 41] }]);
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    expect(service.checkboxState(41, [1])).toBe('indeterminate');
    expect(service.checkboxState(1, [])).toBe('indeterminate');
  });

  it('markerer id-er som mangler i svaret som kjente (ingen evig refetch)', async () => {
    filterState.addTaxon(42);
    TestBed.tick();

    // Backend svarer uten id 42 (f.eks. avkortet eller ukjent hos backend)
    httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry').flush([]);
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    // Ingen ny request — 42 er markert som kjent med tom kjede
    httpTesting.expectNone('/api/Lookup/TaxonAncestry');
  });

  it('chunker ancestry-oppslag til maks 100 id-er per request', async () => {
    const ids = Array.from({ length: 150 }, (_, i) => 1000 + i);
    filterState.setTaxons(ids);
    TestBed.tick();

    const first = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    expect(first.request.params.getAll('taxonIds')!.length).toBe(100);
    first.flush(first.request.params.getAll('taxonIds')!.map((id) => ({ id: Number(id), parentIds: [] })));
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    const second = httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonAncestry');
    expect(second.request.params.getAll('taxonIds')!.length).toBe(50);
    second.flush(second.request.params.getAll('taxonIds')!.map((id) => ({ id: Number(id), parentIds: [] })));
    await new Promise((r) => setTimeout(r, 0));
    TestBed.tick();

    httpTesting.expectNone('/api/Lookup/TaxonAncestry');
  });

  it('materialiserte søsken får forfedrekjeden registrert uten ekstra oppslag', async () => {
    // 6 er ikke utforsket i treet: verken 7 eller 8 er registrert på forhånd
    filterState.setTaxons([6]);
    service.registerAncestry(7, [6]);

    const promise = service.deselect(7, [6]);
    httpTesting
      .expectOne((r) => r.url === '/api/Lookup/TaxonTree' && r.params.get('parentTaxonId') === '6')
      .flush([node(7), node(8)]);
    await promise;
    TestBed.tick();

    // Utvalget er nå [8] — og 8 fikk kjeden [6] registrert under materialiseringen,
    // så ingen TaxonAncestry-request skal gå for den
    expect(filterState.selectedTaxonIds()).toEqual([8]);
    httpTesting.expectNone('/api/Lookup/TaxonAncestry');
  });

  it('resolveAncestries gjør ingenting når alt er kjent', () => {
    filterState.setTaxons([4]);
    service.resolveAncestries();
    httpTesting.expectNone('/api/Lookup/TaxonAncestry');
  });

  it('forelder er indeterminate selv når alle barn på alle nivåer er valgt', () => {
    // Hierarki: 1 -> {2, 3}; 2 -> {4, 5}; 3 -> {7}
    service.registerChildren(3, [node(7)]);
    service.registerAncestry(7, [1, 3]);

    filterState.setTaxons([4, 5, 7]);
    expect(service.checkboxState(2, [1])).toBe('indeterminate');
    expect(service.checkboxState(3, [1])).toBe('indeterminate');
    expect(service.checkboxState(1, [])).toBe('indeterminate');
  });

  it('isInheritedSelected er sann kun for valgte forgjengere', () => {
    filterState.setTaxons([1]);
    expect(service.isInheritedSelected([1, 2])).toBe(true);
    expect(service.isInheritedSelected([2])).toBe(false);
    expect(service.isInheritedSelected([])).toBe(false);
  });

  describe('uten underliggende taksa', () => {
    beforeEach(() => filterState.setIncludeDescendantTaxa(false));

    it('arver ikke checked fra en valgt forgjenger', () => {
      filterState.setTaxons([1]);
      expect(service.checkboxState(1, [])).toBe('checked');
      expect(service.checkboxState(4, [1, 2])).toBe('unchecked');
    });

    it('viser fortsatt indeterminate på forgjengere til en valgt node', () => {
      filterState.setTaxons([4]);
      expect(service.checkboxState(1, [])).toBe('indeterminate');
    });

    it('select av en forelder beholder valgte etterkommere', () => {
      filterState.setTaxons([4, 5]);
      service.select(2);
      expect(filterState.selectedTaxonIds()).toEqual([4, 5, 2]);
    });
  });
});
