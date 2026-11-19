// Visibility rules (hover delay, touch, open menus) live with the .map-control-tooltip styles in styles.css.
const ANCHOR_CLASS = 'map-control-tooltip-anchor';
const KEYBOARD_FOCUS_CLASS = 'map-control-tooltip-anchor--keyboard-focus';
const DISMISSED_CLASS = 'map-control-tooltip-anchor--dismissed';
export const IMMEDIATE_TOOLTIP_CLASS = 'map-control-tooltip--immediate';

// :focus-within also covers browsers that skip focusout when the focused element is removed.
const KEYBOARD_FOCUSED = `.${KEYBOARD_FOCUS_CLASS}:focus-within`;
// Anchors whose tooltip is shown, or waiting out the hover delay. Mouse focus alone shows nothing.
const TRIGGERED_ANCHORS = [
  `.${ANCHOR_CLASS}:hover`,
  KEYBOARD_FOCUSED,
  `.${ANCHOR_CLASS}:focus-within:has(> .${IMMEDIATE_TOOLTIP_CLASS})`,
].join(', ');

let nextTooltipId = 0;

/**
 * Shows the `.map-control-tooltip` inside `anchor` on hover and keyboard focus.
 * Escape hides it until both the pointer and keyboard focus have left the anchor.
 */
export function enableMapControlTooltip(anchor: HTMLElement): void {
  anchor.classList.add(ANCHOR_CLASS);
  anchor.addEventListener('focusin', () => anchor.classList.toggle(KEYBOARD_FOCUS_CLASS, hasVisibleFocus()));
  anchor.addEventListener('focusout', (event) => {
    if (event.relatedTarget instanceof Node && anchor.contains(event.relatedTarget)) return;
    anchor.classList.remove(KEYBOARD_FOCUS_CLASS);
    if (!anchor.matches(':hover')) anchor.classList.remove(DISMISSED_CLASS);
  });
  anchor.addEventListener('mouseleave', () => {
    if (!anchor.matches(KEYBOARD_FOCUSED)) anchor.classList.remove(DISMISSED_CLASS);
  });
  // Passing the same function every time means the browser registers it only once.
  document.addEventListener('keydown', dismissShownTooltipsOnEscape);
}

/** Wraps a control button in a tooltip anchor and links the tooltip as the button's description. */
export function addMapControlTooltip(button: HTMLButtonElement, text: string): HTMLSpanElement {
  const tooltip = document.createElement('span');
  tooltip.className = 'map-control-tooltip';
  tooltip.id = `map-control-tooltip-${nextTooltipId++}`;
  tooltip.setAttribute('role', 'tooltip');
  tooltip.textContent = text;

  const anchor = document.createElement('div');
  button.replaceWith(anchor);
  anchor.append(button, tooltip);
  button.setAttribute('aria-describedby', tooltip.id);
  enableMapControlTooltip(anchor);
  return tooltip;
}

function dismissShownTooltipsOnEscape(event: KeyboardEvent): void {
  if (event.key !== 'Escape' || event.defaultPrevented) return;
  document.querySelectorAll(TRIGGERED_ANCHORS).forEach((anchor) => anchor.classList.add(DISMISSED_CLASS));
}

/** Looks inside shadow roots, since design system buttons keep the focused `<button>` there. */
function hasVisibleFocus(): boolean {
  let focused = document.activeElement;
  while (focused?.shadowRoot?.activeElement) focused = focused.shadowRoot.activeElement;
  return focused?.matches(':focus-visible') ?? false;
}
