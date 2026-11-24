export function createSvgIcon(svg: string): HTMLSpanElement {
  const span = document.createElement('span');
  span.className = 'artskart-control-icon';
  span.setAttribute('aria-hidden', 'true');
  span.innerHTML = svg;
  return span;
}
