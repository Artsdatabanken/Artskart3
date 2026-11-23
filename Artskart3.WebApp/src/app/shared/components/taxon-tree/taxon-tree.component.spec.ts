import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { TaxonTreeComponent } from './taxon-tree.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { TaxonSelectionService } from '../../services/taxon-selection/taxon-selection.service';
import { LoggingService } from '../../logging.service';
import { TaxonTreeNodeDto } from '../../types/api.types';
import { By } from '@angular/platform-browser';

function node(id: number, hasChildren = false, preferredPopularName?: string): TaxonTreeNodeDto {
  return {
    id,
    validScientificName: `Taxon ${id}`,
    preferredPopularName,
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
      imports: [TaxonTreeComponent, TranslateModule.forRoot()],
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

  type AccordionItem = HTMLElement & { open: boolean };

  function accordionItem(root: HTMLElement = fixture.nativeElement): AccordionItem {
    return root.querySelector('adb-accordion-item') as AccordionItem;
  }

  // The accordion item updates `open` and fires a bubbling toggle event when the user clicks it.
  function toggleByUser(item: AccordionItem, open: boolean): void {
    item.open = open;
    item.dispatchEvent(new Event('toggle', { bubbles: true }));
    fixture.detectChanges();
  }

  function expectChildrenRequest(parentTaxonId: number) {
    return httpTesting.expectOne((r) => r.url === '/api/Lookup/TaxonTree' && r.params.get('parentTaxonId') === String(parentTaxonId));
  }

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

  it('viser populærnavn som standard', () => {
    expect(component.displayName(node(1, false, 'Bakterier'))).toBe('Bakterier');
  });

  it('faller tilbake til vitenskapelig navn når populærnavn mangler', () => {
    expect(component.displayName(node(1))).toBe('Taxon 1');
  });

  it('falls back to the scientific name when the popular name is empty', () => {
    expect(component.displayName(node(1, false, ''))).toBe('Taxon 1');
  });

  it('viser vitenskapelig navn når showScientificNames er på', () => {
    fixture.componentRef.setInput('showScientificNames', true);
    expect(component.displayName(node(1, false, 'Bakterier'))).toBe('Taxon 1');
  });

  it('sender showScientificNames videre til neste tre-nivå', () => {
    fixture.componentRef.setInput('showScientificNames', true);
    toggleByUser(accordionItem(), true);
    expectChildrenRequest(1).flush([node(4, false, 'Barn')]);
    fixture.detectChanges();

    const childTree = fixture.debugElement.query(By.directive(TaxonTreeComponent));
    expect(childTree.nativeElement.querySelector('adb-checkbox > span:not([slot])').innerHTML).toBe('Taxon 4');
  });

  describe('revealing the selection', () => {
    it('opens the nodes with selected descendants, level by level', () => {
      taxonSelection.registerAncestry(20, [1, 5]);
      filterState.setTaxons([20]);
      fixture.componentRef.setInput('revealSelection', 1);
      fixture.detectChanges();

      expect(accordionItem().open).toBe(true);
      expectChildrenRequest(1).flush([node(5, true), node(6, true)]);
      fixture.detectChanges();
      expectChildrenRequest(5).flush([node(20)]);
      fixture.detectChanges();

      const childItems = accordionItem().querySelectorAll('adb-accordion-item') as NodeListOf<AccordionItem>;
      expect([...childItems].map((item) => item.open)).toEqual([true, false]);
      httpTesting.expectNone((r) => r.params.get('parentTaxonId') === '6');
    });

    it('leaves a selected node closed, since its own checkbox shows the selection', () => {
      filterState.setTaxons([1]);
      fixture.componentRef.setInput('revealSelection', 1);
      fixture.detectChanges();
      expect(accordionItem().open).toBe(false);
    });

    it('reopens a node the user closed on the next reveal', () => {
      taxonSelection.registerAncestry(10, [1]);
      filterState.setTaxons([10]);
      fixture.componentRef.setInput('revealSelection', 1);
      fixture.detectChanges();
      expectChildrenRequest(1).flush([node(10)]);

      toggleByUser(accordionItem(), false);
      expect(accordionItem().open).toBe(false);

      fixture.componentRef.setInput('revealSelection', 2);
      fixture.detectChanges();
      expect(accordionItem().open).toBe(true);
    });

    it("doesn't carry a reveal into subtrees the user opens", () => {
      fixture.componentRef.setInput('revealSelection', 1);
      fixture.detectChanges();
      toggleByUser(accordionItem(), true);
      expectChildrenRequest(1).flush([node(5, true)]);
      fixture.detectChanges();

      const childTree = fixture.debugElement.query(By.directive(TaxonTreeComponent)).componentInstance as TaxonTreeComponent;
      expect(childTree.revealSelection()).toBe(0);
    });
  });
});
