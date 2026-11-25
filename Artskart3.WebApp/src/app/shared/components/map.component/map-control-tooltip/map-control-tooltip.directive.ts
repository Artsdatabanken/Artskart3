import { Directive, ElementRef, inject } from '@angular/core';
import { enableMapControlTooltip } from './map-control-tooltip';

/** Shows the `<span class="map-control-tooltip">` placed inside the host element. */
@Directive({ selector: '[appMapControlTooltip]' })
export class MapControlTooltipDirective {
  constructor() {
    enableMapControlTooltip(inject<ElementRef<HTMLElement>>(ElementRef).nativeElement);
  }
}
