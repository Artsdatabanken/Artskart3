import { addMapControlTooltip, enableMapControlTooltip, IMMEDIATE_TOOLTIP_CLASS } from './map-control-tooltip';

const KEYBOARD_FOCUS = 'map-control-tooltip-anchor--keyboard-focus';
const DISMISSED = 'map-control-tooltip-anchor--dismissed';

describe('addMapControlTooltip', () => {
  let container: HTMLDivElement;
  let button: HTMLButtonElement;
  let tooltip: HTMLSpanElement;
  let anchor: HTMLElement;

  beforeEach(() => {
    container = document.createElement('div');
    button = document.createElement('button');
    container.appendChild(button);
    document.body.appendChild(container);
    tooltip = addMapControlTooltip(button, 'Zoom closer');
    anchor = button.parentElement!;
  });

  afterEach(() => container.remove());

  const pressEscape = (target: EventTarget = document) => target.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
  // jsdom guesses focus-visible from earlier events, so the browser's answer is stubbed.
  const stubFocusVisible = (element: Element, visible: boolean) =>
    vi.spyOn(element, 'matches').mockImplementation((selector) => selector === ':focus-visible' && visible);
  const stubHover = (hovered: boolean) => {
    const matches = anchor.matches.bind(anchor);
    vi.spyOn(anchor, 'matches').mockImplementation((selector) => (selector === ':hover' ? hovered : matches(selector)));
  };
  const keyboardFocus = () => {
    stubFocusVisible(button, true);
    button.focus();
  };

  it('wraps the button and links the tooltip as its description', () => {
    expect(anchor.parentElement).toBe(container);
    expect(anchor.classList).toContain('map-control-tooltip-anchor');
    expect(Array.from(anchor.children)).toEqual([button, tooltip]);
    expect(tooltip.getAttribute('role')).toBe('tooltip');
    expect(tooltip.textContent).toBe('Zoom closer');
    expect(button.getAttribute('aria-describedby')).toBe(tooltip.id);
  });

  it('gives each tooltip its own id', () => {
    const otherButton = document.createElement('button');
    container.appendChild(otherButton);
    expect(addMapControlTooltip(otherButton, 'Other').id).not.toBe(tooltip.id);
  });

  it('marks keyboard focus until focus leaves', () => {
    keyboardFocus();
    expect(anchor.classList).toContain(KEYBOARD_FOCUS);

    button.blur();
    expect(anchor.classList).not.toContain(KEYBOARD_FOCUS);
  });

  it('does not mark focus from a pointer click', () => {
    stubFocusVisible(button, false);
    button.focus();
    expect(anchor.classList).not.toContain(KEYBOARD_FOCUS);
  });

  it('finds keyboard focus on a button inside a shadow root', () => {
    const host = document.createElement('div');
    const innerButton = document.createElement('button');
    host.attachShadow({ mode: 'open' }).appendChild(innerButton);
    const shadowAnchor = document.createElement('div');
    shadowAnchor.appendChild(host);
    container.appendChild(shadowAnchor);
    enableMapControlTooltip(shadowAnchor);
    stubFocusVisible(innerButton, true);

    innerButton.focus();
    expect(shadowAnchor.classList).toContain(KEYBOARD_FOCUS);
  });

  it('keeps an Escape dismissal while keyboard focus stays, even when the pointer leaves', () => {
    keyboardFocus();
    pressEscape();
    expect(anchor.classList).toContain(DISMISSED);

    anchor.dispatchEvent(new MouseEvent('mouseleave'));
    expect(anchor.classList).toContain(DISMISSED);

    button.blur();
    expect(anchor.classList).not.toContain(DISMISSED);
  });

  it('keeps an Escape dismissal while the pointer stays, even when focus leaves', () => {
    keyboardFocus();
    pressEscape();
    stubHover(true);
    button.blur();
    expect(anchor.classList).toContain(DISMISSED);

    anchor.dispatchEvent(new MouseEvent('mouseleave'));
    expect(anchor.classList).not.toContain(DISMISSED);
  });

  it('does not dismiss a tooltip that only has mouse focus', () => {
    stubFocusVisible(button, false);
    button.focus();
    pressEscape();
    expect(anchor.classList).not.toContain(DISMISSED);
  });

  it('dismisses an immediate tooltip that has mouse focus', () => {
    tooltip.classList.add(IMMEDIATE_TOOLTIP_CLASS);
    stubFocusVisible(button, false);
    button.focus();
    pressEscape();
    expect(anchor.classList).toContain(DISMISSED);
  });

  it('ignores other keys and Escape presses another handler has used', () => {
    keyboardFocus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(anchor.classList).not.toContain(DISMISSED);

    container.addEventListener('keydown', (event) => event.preventDefault(), { once: true });
    pressEscape(button);
    expect(anchor.classList).not.toContain(DISMISSED);
  });

  it('ignores the keyboard-focus class left behind when the focused element is removed', () => {
    const radio = document.createElement('button');
    anchor.appendChild(radio);
    stubFocusVisible(radio, true);
    radio.focus();
    radio.remove();
    // Like Firefox and Safari, jsdom sends no focusout here
    expect(anchor.classList).toContain(KEYBOARD_FOCUS);

    pressEscape();
    expect(anchor.classList).not.toContain(DISMISSED);
    anchor.dispatchEvent(new MouseEvent('mouseleave'));
    expect(anchor.classList).not.toContain(DISMISSED);
  });
});
