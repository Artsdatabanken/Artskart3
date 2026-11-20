import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { MapTypeSelectorComponent } from './map-type-selector.component';

describe('MapTypeSelectorComponent', () => {
  it('selects the map, closes the menu and moves focus back to the toggle button', async () => {
    await TestBed.configureTestingModule({
      imports: [MapTypeSelectorComponent],
      providers: [provideTranslateService()],
    }).compileComponents();
    const fixture = TestBed.createComponent(MapTypeSelectorComponent);
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    document.body.appendChild(element);
    const selected = vi.fn();
    const openChange = vi.fn();
    fixture.componentInstance.mapTypeSelected.subscribe(selected);
    fixture.componentInstance.mapTypesOpenChange.subscribe(openChange);
    // Whether the design system is registered depends on which specs share the worker, so the shadow button may need faking.
    const host = element.querySelector('adb-icon-button') as HTMLElement & { updateComplete?: Promise<unknown> };
    await host.updateComplete;
    const shadow = host.shadowRoot ?? host.attachShadow({ mode: 'open' });
    const toggleButton = shadow.querySelector('button') ?? shadow.appendChild(document.createElement('button'));

    const radioGroup = element.querySelector('adb-radio-group') as HTMLElement & { value: string };
    radioGroup.value = 'nib';
    radioGroup.dispatchEvent(new Event('change', { bubbles: true }));

    expect(selected).toHaveBeenCalledWith('nib');
    expect(openChange).toHaveBeenCalledWith(false);
    expect(shadow.activeElement).toBe(toggleButton);
    element.remove();
  });
});
