import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ObservationTreeComponent } from './observation-tree.component';
import { TranslateModule } from '@ngx-translate/core';
import { buildObservationTree } from '../observation-list.component/observation-list.model';
import { By } from '@angular/platform-browser';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';

describe('ObservationTreeComponent', () => {
  let component: ObservationTreeComponent;
  let fixture: ComponentFixture<ObservationTreeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ObservationTreeComponent, TranslateModule.forRoot()]
    })
      .compileComponents();

    fixture = TestBed.createComponent(ObservationTreeComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('nodes', buildObservationTree([
      { id: 1, taxonId: 1, displayName: 'Lav', taxonGroupId: 1, taxonGroupName: 'Lav', categoryCode: 'VU', categoryTypeId: 1 },
      { id: 2, taxonId: 2, displayName: 'Sopp', taxonGroupId: 2, taxonGroupName: 'Sopp' },
    ], 'taxonGroup', 'no', (key) => key, () => '2024'));
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  const rows = (): HTMLElement[] => [...fixture.nativeElement.querySelectorAll('[role="treeitem"]')];
  const press = async (element: HTMLElement, key: string) => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    await fixture.whenStable();
  };

  it('starts with headings open and lazy status children collapsed', () => {
    expect(rows()).toHaveLength(4);
    expect(rows()[0].getAttribute('aria-expanded')).toBe('true');
    expect(rows()[1].getAttribute('aria-expanded')).toBe('false');
    expect(rows().filter((row) => row.tabIndex === 0)).toHaveLength(1);
  });

  it('pins headings open for clicks and keyboard collapse', async () => {
    rows()[0].click();
    await fixture.whenStable();
    await press(rows()[0], 'ArrowLeft');
    expect(rows()[0].getAttribute('aria-expanded')).toBe('true');
  });

  it('uses the shared badge for species while keeping status indicators as dots', async () => {
    rows()[1].click();
    await fixture.whenStable();
    const badges = fixture.debugElement.queryAll(By.directive(RiskCategoryBadgeComponent));
    expect(badges).toHaveLength(1);
    expect(badges[0].injector.get(RiskCategoryBadgeComponent).code()).toBe('VU');
    expect(badges[0].nativeElement.getAttribute('aria-label')).toBe('VU: observationList.categories.VU');
    expect(fixture.nativeElement.querySelector('.dot.VU')).not.toBeNull();
  });

  it('expands whole rows, supports keyboard toggling, and leaves observations inactive', async () => {
    rows()[1].click();
    await fixture.whenStable();
    expect(rows()).toHaveLength(5);
    await press(rows()[2], 'Enter');
    expect(rows()).toHaveLength(6);
    expect(rows()[3].classList.contains('leaf')).toBe(true);
    await press(rows()[3], 'Enter');
    expect(rows()[3].getAttribute('aria-selected')).toBeNull();
    rows()[3].focus();
    fixture.componentRef.setInput('resetKey', 'new-selection');
    await fixture.whenStable();
    expect(rows()).toHaveLength(4);
    expect(document.activeElement).toBe(rows()[0]);
  });

  it('navigates across groups with arrow keys and Home/End', async () => {
    rows()[0].click();
    await fixture.whenStable();
    await press(rows()[0], 'ArrowDown');
    expect(document.activeElement).toBe(rows()[1]);
    await press(rows()[1], 'End');
    expect(document.activeElement).toBe(rows().at(-1));
    await press(rows().at(-1)!, 'Home');
    expect(document.activeElement).toBe(rows()[0]);
  });

  it('keeps independent branches open and returns to the parent on Left', async () => {
    rows()[1].click();
    await fixture.whenStable();
    rows().at(-1)!.click();
    await fixture.whenStable();
    expect(rows().filter((row) => !row.classList.contains('group-heading') && row.getAttribute('aria-expanded') === 'true')).toHaveLength(2);
    await press(rows().at(-1)!, 'Home');
    await press(rows()[0], 'ArrowDown');
    await press(rows()[1], 'ArrowRight');
    await press(rows()[2], 'ArrowLeft');
    expect(document.activeElement).toBe(rows()[1]);
  });
});
