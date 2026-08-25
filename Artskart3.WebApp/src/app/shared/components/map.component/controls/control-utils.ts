export function createSvgIcon(svg: string): HTMLSpanElement {
  const span = document.createElement('span');
  span.innerHTML = svg;
  return span;
}
