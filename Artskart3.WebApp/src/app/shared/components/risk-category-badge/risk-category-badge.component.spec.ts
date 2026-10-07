import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RiskCategoryBadgeComponent } from './risk-category-badge.component';

describe('RiskCategoryBadgeComponent', () => {
  let component: RiskCategoryBadgeComponent;
  let fixture: ComponentFixture<RiskCategoryBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RiskCategoryBadgeComponent]
    })
      .compileComponents();

    fixture = TestBed.createComponent(RiskCategoryBadgeComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('code', 'VU');
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('uses the sidebar circle and typography', () => {
    const circle: HTMLElement = fixture.nativeElement.querySelector('.risk-category-circle-small');
    const text: HTMLElement = circle.querySelector('.risk-category-tag-label-small')!;
    expect(circle.classList.contains('VU')).toBe(true);
    expect(text.textContent).toBe('VU');
    expect(getComputedStyle(circle).width).toBe('24px');
    expect(getComputedStyle(circle).height).toBe('24px');
    expect(getComputedStyle(text).fontSize).toBe('12px');
    expect(getComputedStyle(text).lineHeight).toBe('24px');
  });

  it('updates the category without retaining the previous color class', async () => {
    fixture.componentRef.setInput('code', 'SE');
    await fixture.whenStable();
    const circle: HTMLElement = fixture.nativeElement.querySelector('.risk-category-circle-small');
    expect(circle.classList.contains('SE')).toBe(true);
    expect(circle.classList.contains('VU')).toBe(false);
    expect(circle.textContent?.trim()).toBe('SE');
  });

  it('provides an optional accessible label and tooltip', async () => {
    expect(fixture.nativeElement.getAttribute('role')).toBeNull();
    fixture.componentRef.setInput('label', 'VU: Sårbar');
    await fixture.whenStable();
    expect(fixture.nativeElement.getAttribute('role')).toBe('img');
    expect(fixture.nativeElement.getAttribute('aria-label')).toBe('VU: Sårbar');
    expect(fixture.nativeElement.getAttribute('title')).toBe('VU: Sårbar');
  });
});
