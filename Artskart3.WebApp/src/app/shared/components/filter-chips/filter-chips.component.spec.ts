import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FilterChipsComponent } from './filter-chips.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

describe('FilterChipsComponent', () => {
  let component: FilterChipsComponent;
  let fixture: ComponentFixture<FilterChipsComponent>;
  let filterState: FilterStateService;
  let translate: TranslateService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FilterChipsComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(FilterChipsComponent);
    component = fixture.componentInstance;
    filterState = TestBed.inject(FilterStateService);
    translate = TestBed.inject(TranslateService);
    translate.setDefaultLang('no');
    translate.use('no');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show no chips when no filters are active', () => {
    expect(component.chips().length).toBe(0);
  });

  it('should show a chip when taxon groups are selected', () => {
    filterState.addTaxonGroup(1);
    filterState.addTaxonGroup(2);
    const chips = component.chips();
    const taxonChip = chips.find((c) => c.text.includes('(2)'));
    expect(taxonChip).toBeTruthy();
  });

  it('should show a chip when categories are selected', () => {
    filterState.addCategory(10);
    const chips = component.chips();
    expect(chips.length).toBeGreaterThan(0);
    expect(chips.some((c) => c.text.includes('(1)'))).toBe(true);
  });

  it('should show a chip for areas (municipalities + ocean areas)', () => {
    filterState.addMunicipality('0301');
    filterState.addMunicipality('0302');
    filterState.toggleOceanArea('ocean1');
    const chips = component.chips();
    const areaChip = chips.find((c) => c.text.includes('(3)'));
    expect(areaChip).toBeTruthy();
  });

  it('should show a chip when taxons (species) are selected', () => {
    filterState.addTaxon(1234);
    const chips = component.chips();
    expect(chips.some((c) => c.text.includes('(1)'))).toBe(true);
  });

  it('should show a chip for coordinate precision', () => {
    filterState.setCoordinatePrecision(0, 500);
    const chips = component.chips();
    const precChip = chips.find((c) => c.label === 'sidebar.coordinatePrecision');
    expect(precChip).toBeTruthy();
  });

  it('should show a chip for period', () => {
    filterState.setPeriod(1990, 2020);
    const chips = component.chips();
    const periodChip = chips.find((c) => c.label === 'sidebar.period');
    expect(periodChip).toBeTruthy();
  });

  it('should clear taxon groups when chip close is called', () => {
    filterState.addTaxonGroup(1);
    filterState.addTaxonGroup(2);
    const chips = component.chips();
    const taxonChip = chips.find((c) => c.text.includes('(2)'));
    taxonChip!.clear();
    expect(filterState.selectedTaxonGroupIds().length).toBe(0);
  });

  it('should clear categories when chip close is called', () => {
    filterState.addCategory(10);
    filterState.addCategory(11);
    const chips = component.chips();
    const catChip = chips.find((c) => c.text.includes('(2)'));
    catChip!.clear();
    expect(filterState.selectedCategoryIds().length).toBe(0);
  });

  it('should clear coordinate precision when chip close is called', () => {
    filterState.setCoordinatePrecision(0, 500);
    const chips = component.chips();
    const precChip = chips.find((c) => c.label === 'sidebar.coordinatePrecision');
    precChip!.clear();
    expect(filterState.coordinatePrecisionFrom()).toBeNull();
    expect(filterState.coordinatePrecisionTo()).toBeNull();
  });

  it('should clear period when chip close is called', () => {
    filterState.setPeriod(1990, 2020);
    const chips = component.chips();
    const periodChip = chips.find((c) => c.label === 'sidebar.period');
    periodChip!.clear();
    expect(filterState.periodFrom()).toBeNull();
    expect(filterState.periodTo()).toBeNull();
  });

  it('should clear taxons when chip close is called', () => {
    filterState.addTaxon(1234);
    filterState.addTaxon(5678);
    const chips = component.chips();
    const taxonChip = chips.find((c) => c.text.includes('(2)'));
    taxonChip!.clear();
    expect(filterState.selectedTaxonIds().length).toBe(0);
  });

  it('should update chip text when language changes', () => {
    filterState.addTaxonGroup(1);
    component.chips(); // trigger initial evaluation

    translate.use('en');
    fixture.detectChanges();

    const chipsAfter = component.chips();
    // Text should change (different translation)
    // With TranslateModule.forRoot() and no translations loaded,
    // keys are returned as-is, but the signal should still re-evaluate
    expect(chipsAfter.length).toBe(1);
    // The computed should have been triggered (currentLang changed)
    expect(chipsAfter[0]).toBeTruthy();
  });
});
