import { Component, DestroyRef, ElementRef, effect, inject, input, signal, viewChild } from '@angular/core';

let nextInstanceId = 0;

const GapPx = 8;
// Keeps the arrow off the tooltip's rounded corners.
const ArrowMarginPx = 12;
// Time to move the pointer from the anchor onto the tooltip before it hides.
const HideDelayMs = 150;

/**
 * Describes its anchor in a tooltip shown while the anchor is hovered or has keyboard focus.
 * The tooltip can itself be hovered and is dismissed with Escape (WCAG 1.4.13). It is shown in
 * the top layer, so a narrow, scrolling parent such as the sidebar can't clip it.
 */
@Component({
  selector: 'app-tooltip',
  template: '<span #arrow class="tooltip-arrow" aria-hidden="true"></span><ng-content />',
  styleUrl: './tooltip.component.css',
  host: {
    role: 'tooltip',
    popover: 'manual',
    '[id]': 'id',
    '(pointerenter)': 'setHovered(true)',
    '(pointerleave)': 'setHovered(false)',
  },
})
export class TooltipComponent {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly doc = this.element.ownerDocument;
  private readonly arrow = viewChild.required<ElementRef<HTMLElement>>('arrow');

  readonly anchor = input.required<HTMLElement>();
  /** The element to place the tooltip beside, when that is more than the anchor. */
  readonly alignTo = input<HTMLElement>();
  readonly id = `tooltip-${nextInstanceId++}`;
  readonly visible = signal(false);

  private hovered = false;
  private focused = false;
  private hideTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect((onCleanup) => {
      const anchor = this.anchor();
      const onPointerEnter = (event: PointerEvent) => {
        if (event.pointerType !== 'touch') this.setHovered(true);
      };
      const onPointerLeave = () => this.setHovered(false);
      // A mouse click focuses without :focus-visible, and the pointer already shows the tooltip then.
      const onFocus = () => {
        if (anchor.matches(':focus-visible')) this.setFocused(true);
      };
      const onBlur = () => this.setFocused(false);

      anchor.setAttribute('aria-describedby', this.id);
      anchor.addEventListener('pointerenter', onPointerEnter);
      anchor.addEventListener('pointerleave', onPointerLeave);
      anchor.addEventListener('focus', onFocus);
      anchor.addEventListener('blur', onBlur);
      onCleanup(() => {
        anchor.removeAttribute('aria-describedby');
        anchor.removeEventListener('pointerenter', onPointerEnter);
        anchor.removeEventListener('pointerleave', onPointerLeave);
        anchor.removeEventListener('focus', onFocus);
        anchor.removeEventListener('blur', onBlur);
      });
    });

    inject(DestroyRef).onDestroy(() => this.dismiss());
  }

  setHovered(hovered: boolean): void {
    this.hovered = hovered;
    this.update();
  }

  private setFocused(focused: boolean): void {
    this.focused = focused;
    this.update();
  }

  private update(): void {
    clearTimeout(this.hideTimer);
    if (this.hovered || this.focused) {
      this.show();
    } else {
      this.hideTimer = setTimeout(() => this.dismiss(), HideDelayMs);
    }
  }

  private show(): void {
    if (!this.visible()) {
      this.visible.set(true);
      if (this.element.popover) this.element.showPopover();
      this.doc.addEventListener('keydown', this.onKeydown);
      this.doc.addEventListener('scroll', this.onScroll, { capture: true, passive: true });
    }
    this.position();
  }

  private readonly dismiss = () => {
    clearTimeout(this.hideTimer);
    this.hovered = false;
    this.focused = false;
    if (!this.visible()) return;
    this.visible.set(false);
    if (this.element.popover && this.element.matches(':popover-open')) this.element.hidePopover();
    this.doc.removeEventListener('keydown', this.onKeydown);
    this.doc.removeEventListener('scroll', this.onScroll, { capture: true });
  };

  // Placed against the viewport, so it can't follow the anchor when something around it scrolls.
  private readonly onScroll = (event: Event) => {
    const scrolled = event.target;
    if (scrolled === this.doc || (scrolled instanceof Node && scrolled.contains(this.anchor()))) this.dismiss();
  };

  private readonly onKeydown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') this.dismiss();
  };

  /** To the right of the target, or below it when there is no room, kept within the screen. */
  private position(): void {
    // Measured at the corner, so the size isn't squeezed by where it was placed last time.
    this.element.style.left = '0px';
    this.element.style.top = '0px';
    const target = (this.alignTo() ?? this.anchor()).getBoundingClientRect();
    const tooltip = this.element.getBoundingClientRect();
    const { clientWidth, clientHeight } = this.doc.documentElement;

    const placement = target.right + GapPx + tooltip.width <= clientWidth - GapPx ? 'right' : 'bottom';
    const left = placement === 'right' ? target.right + GapPx : clamp(target.left, GapPx, clientWidth - tooltip.width - GapPx);
    const top = clamp(
      placement === 'right' ? target.top + (target.height - tooltip.height) / 2 : target.bottom + GapPx,
      GapPx,
      clientHeight - tooltip.height - GapPx,
    );
    this.element.style.left = `${left}px`;
    this.element.style.top = `${top}px`;
    this.element.setAttribute('data-placement', placement);

    // Points at the middle of the target, also when the tooltip has been moved to stay on screen.
    const arrow = this.arrow().nativeElement.style;
    if (placement === 'right') {
      arrow.top = `${clamp(target.top + target.height / 2 - top, ArrowMarginPx, tooltip.height - ArrowMarginPx)}px`;
      arrow.left = '';
    } else {
      arrow.left = `${clamp(target.left + target.width / 2 - left, ArrowMarginPx, tooltip.width - ArrowMarginPx)}px`;
      arrow.top = '';
    }
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(value, max));
}
