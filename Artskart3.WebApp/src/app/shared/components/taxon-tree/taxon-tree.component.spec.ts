import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TaxonTreeComponent } from './taxon-tree.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { TaxonSelectionService } from '../../services/taxon-selection/taxon-selection.service';
import { LoggingService } from '../../logging.service';
import { TaxonTreeNodeDto } from '../../types/api.types';

function node(id: number, hasChildren = false): TaxonTreeNodeDto {
  return {
    id,
    validScientificName: `Taxon ${id}`,
    taxonRankId: 10,
    taxonGroupId: 1,
    existsInCountry: true,
    hasChildren,
    children: [],
  };
}

describe('TaxonTreeComponent', () => {
  let component: TaxonTreeComponent;
  let fixture: ComponentFixture<TaxonTreeComponent>;
  let filterState: FilterStateService;
  let taxonSelection: TaxonSelectionService;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaxonTreeComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    filterState = TestBed.inject(FilterStateService);
    taxonSelection = TestBed.inject(TaxonSelectionService);
    httpTesting = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(TaxonTreeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonTree').flush([node(1, true), node(2), node(3)]);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('rendrer alle noder unchecked som standard', () => {
    expect(component.checkboxState(1)).toBe('unchecked');
    expect(component.checkboxState(2)).toBe('unchecked');
  });

  it('markerer valgt node som checked', () => {
    filterState.setTaxons([2]);
    fixture.detectChanges();
    expect(component.checkboxState(2)).toBe('checked');
  });

  it('markerer forgjenger til valgt node som indeterminate', () => {
    taxonSelection.registerAncestry(10, [1]);
    filterState.setTaxons([10]);
    fixture.detectChanges();
    expect(component.checkboxState(1)).toBe('indeterminate');
    expect(component.checkboxState(2)).toBe('unchecked');
  });

  it('markerer etterkommere av valgt forgjenger som checked (arvet)', () => {
    // Simuler et barnnivå under node 1 med ancestorIds=[1]
    fixture.componentRef.setInput('ancestorIds', [1]);
    filterState.setTaxons([1]);
    fixture.detectChanges();
    expect(component.checkboxState(2)).toBe('checked');
  });

  it('klikk på unchecked node velger den', () => {
    component.onTaxonToggle(2);
    expect(filterState.selectedTaxonIds()).toEqual([2]);
  });

  it('klikk på checked node fjerner valget', async () => {
    filterState.setTaxons([2]);
    fixture.detectChanges();
    await component.onTaxonToggle(2);
    expect(filterState.selectedTaxonIds()).toEqual([]);
  });

  it('klikk på indeterminate node velger den og fjerner kjente etterkommere', () => {
    taxonSelection.registerAncestry(10, [1]);
    taxonSelection.registerAncestry(11, [1]);
    filterState.setTaxons([10, 11]);
    fixture.detectChanges();

    component.onTaxonToggle(1);
    expect(filterState.selectedTaxonIds()).toEqual([1]);
  });

  it('avhaking av arvet checked node materialiserer søsknene', async () => {
    // Simuler barnnivået under node 1: noder med ancestorIds=[1]
    fixture.componentRef.setInput('ancestorIds', [1]);
    filterState.setTaxons([1]);
    fixture.detectChanges();

    const toggle = component.onTaxonToggle(2);
    // Søskenlista under 1 er ikke i cachen — hentes fra API
    httpTesting
      .expectOne((r) => r.url === '/api/Lookup/TaxonTree' && r.params.get('parentTaxonId') === '1')
      .flush([node(2), node(3)]);
    await toggle;

    expect(filterState.selectedTaxonIds()).toEqual([3]);
  });

  it('childAncestorIds returnerer samme instans ved gjentatte kall', () => {
    expect(component.childAncestorIds(1)).toBe(component.childAncestorIds(1));
  });

  it('childAncestorIds invalideres når ancestorIds-inputen endrer referanse', () => {
    const first = component.childAncestorIds(1);
    fixture.componentRef.setInput('ancestorIds', [1]);
    const second = component.childAncestorIds(1);
    expect(second).not.toBe(first);
    expect(second).toEqual([1, 1]);
  });

  it('onTaxonToggle fanger feil fra deselect og logger dem', async () => {
    const logger = TestBed.inject(LoggingService);
    const errorSpy = vi.spyOn(logger, 'error').mockImplementation(() => undefined);
    vi.spyOn(taxonSelection, 'deselect').mockRejectedValue(new Error('nettverksfeil'));

    filterState.setTaxons([2]);
    fixture.detectChanges();

    await expect(component.onTaxonToggle(2)).resolves.toBeUndefined();
    expect(errorSpy).toHaveBeenCalled();
  });

  it('tre-lastede noder trenger ikke ancestry-oppslag', () => {
    filterState.setTaxons([2]);
    taxonSelection.resolveAncestries();
    httpTesting.expectNone('/api/Lookup/TaxonAncestry');
  });

  it('sender ancestorIds videre til neste tre-nivå', () => {
    expect(component.childAncestorIds(1)).toEqual([1]);
    expect(component.ancestorIds()).toEqual([]);
  });
});
