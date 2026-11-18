import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TooltipComponent } from './tooltip.component';

@Component({
  imports: [TooltipComponent],
  template: `
    <button #anchor type="button">Art (2)</button>
    <app-tooltip [anchor]="anchor">Cnidaria, Nanaloricida</app-tooltip>
  `,
})
class HostComponent {}

describe('TooltipComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let tooltip: TooltipComponent;
  let tooltipElement: HTMLElement;
  let anchor: HTMLButtonElement;

  beforeEach(async () => {
    vi.useFakeTimers();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const tooltipDebug = fixture.debugElement.query(By.directive(TooltipComponent));
    tooltip = tooltipDebug.componentInstance;
    tooltipElement = tooltipDebug.nativeElement;
    anchor = fixture.nativeElement.querySelector('button');
  });

  afterEach(() => vi.useRealTimers());

  function pointer(target: HTMLElement, type: 'pointerenter' | 'pointerleave', pointerType = 'mouse'): void {
    target.dispatchEvent(Object.assign(new Event(type), { pointerType }));
  }

  it('describes its anchor', () => {
    expect(tooltipElement.getAttribute('role')).toBe('tooltip');
    expect(anchor.getAttribute('aria-describedby')).toBe(tooltipElement.id);
    expect(tooltipElement.textContent).toContain('Cnidaria, Nanaloricida');
  });

  it('shows while the anchor is hovered and hides shortly after the pointer leaves', () => {
    pointer(anchor, 'pointerenter');
    expect(tooltip.visible()).toBe(true);

    pointer(anchor, 'pointerleave');
    expect(tooltip.visible()).toBe(true);
    vi.advanceTimersByTime(200);
    expect(tooltip.visible()).toBe(false);
  });

  it('stays while the pointer moves from the anchor onto the tooltip', () => {
    pointer(anchor, 'pointerenter');
    pointer(anchor, 'pointerleave');
    pointer(tooltipElement, 'pointerenter');
    vi.advanceTimersByTime(200);
    expect(tooltip.visible()).toBe(true);

    pointer(tooltipElement, 'pointerleave');
    vi.advanceTimersByTime(200);
    expect(tooltip.visible()).toBe(false);
  });

  it("doesn't show for a touch, which opens the filter instead", () => {
    pointer(anchor, 'pointerenter', 'touch');
    expect(tooltip.visible()).toBe(false);
  });

  it('is dismissed with Escape while hovered', () => {
    pointer(anchor, 'pointerenter');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(tooltip.visible()).toBe(false);
  });

  it('is dismissed when the anchor scrolls, since it is placed against the viewport', () => {
    pointer(anchor, 'pointerenter');
    fixture.nativeElement.dispatchEvent(new Event('scroll'));
    expect(tooltip.visible()).toBe(false);
  });

  it('stays when something elsewhere on the page scrolls', () => {
    pointer(anchor, 'pointerenter');
    const elsewhere = document.body.appendChild(document.createElement('div'));
    elsewhere.dispatchEvent(new Event('scroll'));
    elsewhere.remove();
    expect(tooltip.visible()).toBe(true);
  });

  it('shows on keyboard focus but not on the focus a mouse click gives', () => {
    const matches = vi.spyOn(anchor, 'matches').mockReturnValue(false);
    anchor.dispatchEvent(new FocusEvent('focus'));
    expect(tooltip.visible()).toBe(false);

    matches.mockReturnValue(true);
    anchor.dispatchEvent(new FocusEvent('focus'));
    expect(tooltip.visible()).toBe(true);

    anchor.dispatchEvent(new FocusEvent('blur'));
    vi.advanceTimersByTime(200);
    expect(tooltip.visible()).toBe(false);
  });

  describe('placement', () => {
    function layout(
      anchorRect: Partial<DOMRect>,
      tooltipSize: { width: number; height: number },
      viewport: { width: number; height: number },
    ) {
      vi.spyOn(anchor, 'getBoundingClientRect').mockReturnValue({ ...new DOMRect(), ...anchorRect } as DOMRect);
      vi.spyOn(tooltipElement, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, tooltipSize.width, tooltipSize.height));
      vi.spyOn(document.documentElement, 'clientWidth', 'get').mockReturnValue(viewport.width);
      vi.spyOn(document.documentElement, 'clientHeight', 'get').mockReturnValue(viewport.height);
    }

    const arrow = () => tooltipElement.querySelector<HTMLElement>('.tooltip-arrow')!;

    it('sits to the right of the anchor with the arrow at its middle', () => {
      layout(
        { left: 10, right: 110, top: 100, bottom: 130, width: 100, height: 30 },
        { width: 200, height: 50 },
        { width: 1000, height: 800 },
      );
      pointer(anchor, 'pointerenter');

      expect(tooltipElement.dataset['placement']).toBe('right');
      expect(tooltipElement.style.left).toBe('118px');
      expect(tooltipElement.style.top).toBe('90px');
      expect(arrow().style.top).toBe('25px');
    });

    it('goes below the anchor when there is no room to the right', () => {
      layout(
        { left: 10, right: 110, top: 100, bottom: 130, width: 100, height: 30 },
        { width: 200, height: 50 },
        { width: 250, height: 800 },
      );
      pointer(anchor, 'pointerenter');

      expect(tooltipElement.dataset['placement']).toBe('bottom');
      expect(tooltipElement.style.left).toBe('10px');
      expect(tooltipElement.style.top).toBe('138px');
      expect(arrow().style.left).toBe('50px');
    });

    it('keeps pointing at the anchor when it is moved to stay on screen', () => {
      layout(
        { left: 10, right: 110, top: 4, bottom: 34, width: 100, height: 30 },
        { width: 200, height: 80 },
        { width: 1000, height: 800 },
      );
      pointer(anchor, 'pointerenter');

      expect(tooltipElement.style.top).toBe('8px');
      expect(arrow().style.top).toBe('12px');
    });
  });
});
