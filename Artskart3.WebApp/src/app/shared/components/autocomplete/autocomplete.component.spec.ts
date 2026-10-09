import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AutocompleteComponent } from './autocomplete.component';
import { AutocompleteOptionDirective } from './autocomplete-option.directive';

interface Item {
  id: number;
  name: string;
}

@Component({
  imports: [AutocompleteComponent, AutocompleteOptionDirective],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  template: `
    <div class="scroller" style="overflow-y: auto">
      <app-autocomplete [search]="search" label="Søk" [trackBy]="trackById" [fitContent]="fitContent()" (selected)="selected.push($event)">
        <ng-template [appAutocompleteOption]="search" let-item let-term="term"
          ><span class="item-name">{{ item.name }}</span
          ><span class="item-term">{{ term }}</span></ng-template
        >
      </app-autocomplete>
    </div>
    <button type="button" class="outside">Outside</button>
  `,
})
class TestHostComponent {
  readonly search = vi.fn<(term: string) => Observable<Item[]>>(() => of([]));
  readonly fitContent = signal(false);
  readonly trackById = (item: Item) => item.id;
  readonly selected: Item[] = [];
}

describe('AutocompleteComponent', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let host: TestHostComponent;
  let component: AutocompleteComponent<Item>;
  let element: HTMLElement;

  const items: Item[] = [
    { id: 1, name: 'Kjøttmeis' },
    { id: 2, name: 'Blåmeis' },
  ];

  const input = (value: string) => component.onSearchInput(new CustomEvent('adb-input', { detail: { value } }) as unknown as Event);
  const options = () => [...element.querySelectorAll<HTMLElement>('.autocomplete-item')];
  const activeElement = () => element.ownerDocument.activeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent, TranslateModule.forRoot()],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    const autocomplete = fixture.debugElement.query(By.directive(AutocompleteComponent));
    component = autocomplete.componentInstance;
    element = autocomplete.nativeElement;
  });

  it('should hide the popup when input is shorter than minLength', () => {
    input('k');
    expect(component.showAutocomplete()).toBe(false);
    expect(component.results().length).toBe(0);
  });

  it('should hide the popup and clear results on clear', () => {
    component.results.set(items);
    component.showAutocomplete.set(true);
    component.searchTerm.set('kj');

    component.onSearchClear();

    expect(component.showAutocomplete()).toBe(false);
    expect(component.results().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should emit the item and clear the search state on select', () => {
    component.results.set(items);
    component.showAutocomplete.set(true);
    component.searchTerm.set('kj');

    component.select(items[0]);

    expect(host.selected).toEqual([items[0]]);
    expect(component.showAutocomplete()).toBe(false);
    expect(component.results().length).toBe(0);
    expect(component.searchTerm()).toBe('');
  });

  it('should select the first result on submit', () => {
    component.results.set(items);
    component.showAutocomplete.set(true);
    component.onSearchSubmit();
    expect(host.selected).toEqual([items[0]]);
  });

  it('should do nothing on submit when there are no results', () => {
    component.onSearchSubmit();
    expect(host.selected).toEqual([]);
  });

  it('should pass the item and the search term to the option template', () => {
    component.searchTerm.set('meis');
    component.results.set(items);
    component.showAutocomplete.set(true);
    fixture.detectChanges();
    expect(options().map((o) => o.querySelector('.item-name')?.textContent)).toEqual(['Kjøttmeis', 'Blåmeis']);
    expect(options()[0].querySelector('.item-term')?.textContent).toBe('meis');
  });

  it('should fire a new request when retyping the same term after a selection', () => {
    vi.useFakeTimers();
    try {
      host.search.mockReturnValue(of(items));

      input('kj');
      vi.advanceTimersByTime(300);
      expect(host.search).toHaveBeenCalledTimes(1);

      component.select(items[0]);
      input('kj');
      vi.advanceTimersByTime(300);
      expect(host.search).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should not reopen the popup when an in-flight response lands after Escape', () => {
    vi.useFakeTimers();
    try {
      const pending = new Subject<Item[]>();
      host.search.mockReturnValue(pending.asObservable());
      component.results.set(items);
      component.showAutocomplete.set(true);

      input('kjø');
      vi.advanceTimersByTime(300);
      component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));
      expect(component.showAutocomplete()).toBe(false);

      pending.next(items);
      expect(component.showAutocomplete()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should cancel an in-flight request and stay closed when cleared', () => {
    vi.useFakeTimers();
    try {
      const pending = new Subject<Item[]>();
      host.search.mockReturnValue(pending.asObservable());

      input('kjø');
      vi.advanceTimersByTime(300);
      component.onSearchClear();
      vi.advanceTimersByTime(0); // only the 0 ms path can have fired
      expect(pending.observed).toBe(false); // inner subscription torn down
      pending.next(items); // late response is a no-op
      expect(component.showAutocomplete()).toBe(false);
      expect(component.results().length).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should keep dismissed results fresh for an ArrowDown reopen', () => {
    vi.useFakeTimers();
    try {
      const pending = new Subject<Item[]>();
      host.search.mockReturnValue(pending.asObservable());

      input('kjø');
      vi.advanceTimersByTime(300);
      component.results.set(items);
      component.showAutocomplete.set(true);
      component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true }));

      const fresh = [{ id: 9999, name: 'Kjøttmeis' }];
      pending.next(fresh);
      expect(component.showAutocomplete()).toBe(false);
      expect(component.results()[0].id).toBe(9999);
    } finally {
      vi.useRealTimers();
    }
  });

  it('should restore focus to the first option when a response replaces the list', () => {
    vi.useFakeTimers();
    try {
      const first = new Subject<Item[]>();
      const second = new Subject<Item[]>();
      host.search.mockReturnValueOnce(first.asObservable()).mockReturnValueOnce(second.asObservable());

      input('kj');
      vi.advanceTimersByTime(300);
      first.next(items);
      fixture.detectChanges();
      element.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', cancelable: true, bubbles: true }));
      fixture.detectChanges();
      expect(activeElement()).toBe(options()[0]);

      input('kjø');
      vi.advanceTimersByTime(300);
      second.next([{ id: 9999, name: 'Blåmeis' }]);
      fixture.detectChanges();

      expect(component.highlightedIndex()).toBe(0);
      expect(activeElement()).toBe(options()[0]);
      expect(options()[0].textContent).toContain('Blåmeis');
    } finally {
      vi.useRealTimers();
    }
  });

  it('should stay usable after a failed request instead of showing no results', () => {
    vi.useFakeTimers();
    try {
      host.search.mockReturnValueOnce(throwError(() => new Error('500'))).mockReturnValueOnce(of(items));

      input('kj');
      vi.advanceTimersByTime(300);
      expect(component.showNoResults()).toBe(false);
      expect(component.showAutocomplete()).toBe(false);

      input('kjø');
      vi.advanceTimersByTime(300);
      expect(component.showAutocomplete()).toBe(true);
      expect(component.results()).toEqual(items);
    } finally {
      vi.useRealTimers();
    }
  });

  describe('focus leaving and returning', () => {
    // The popup closes a microtask after focusout; see onFocusout.
    const focusout = async (relatedTarget: EventTarget | null, target: Element = element) => {
      target.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget }));
      await Promise.resolve();
    };
    const focusin = (relatedTarget: EventTarget | null) =>
      element.dispatchEvent(new FocusEvent('focusin', { bubbles: true, relatedTarget }));
    const escape = () => element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', cancelable: true, bubbles: true }));
    // Stands in for adb-search's shadow DOM, which isn't rendered in tests.
    const searchShadow = () => {
      const search = element.querySelector('adb-search') as HTMLElement;
      const shadow = search.shadowRoot ?? search.attachShadow({ mode: 'open' });
      shadow.innerHTML = '<input /><button type="button">Søk</button>';
      return shadow;
    };
    const click = (target: Element) => target.dispatchEvent(new MouseEvent('click', { bubbles: true, composed: true }));

    beforeEach(() => {
      component.results.set(items);
      component.showAutocomplete.set(true);
      fixture.detectChanges();
    });

    it('should close the popup when focus moves outside', async () => {
      await focusout(fixture.nativeElement.querySelector('.outside'));
      expect(component.showAutocomplete()).toBe(false);
    });

    it('should close the popup when focus leaves the page', async () => {
      await focusout(null);
      expect(component.showAutocomplete()).toBe(false);
    });

    it('should stay open when focus moves to an option', async () => {
      await focusout(options()[1]);
      expect(component.showAutocomplete()).toBe(true);
    });

    // Chrome fires focusout, with no relatedTarget, when new results remove the focused option.
    it('should stay open when the focused option is removed by new results', async () => {
      const option = options()[0];
      option.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: null }));
      option.remove();
      await Promise.resolve();

      expect(component.showAutocomplete()).toBe(true);
    });

    it('should not reopen when an in-flight response lands after focus left', async () => {
      vi.useFakeTimers();
      try {
        const pending = new Subject<Item[]>();
        host.search.mockReturnValue(pending.asObservable());
        input('kjø');
        vi.advanceTimersByTime(300);
        await focusout(null);

        pending.next(items);
        expect(component.showAutocomplete()).toBe(false);
      } finally {
        vi.useRealTimers();
      }
    });

    it('should drop older results when the request fails after focus left', async () => {
      vi.useFakeTimers();
      try {
        const pending = new Subject<Item[]>();
        host.search.mockReturnValue(pending.asObservable());
        input('kjø');
        vi.advanceTimersByTime(300);
        await focusout(null);

        pending.error(new Error('500'));
        focusin(null);

        expect(component.results()).toEqual([]);
        expect(component.showAutocomplete()).toBe(false);
      } finally {
        vi.useRealTimers();
      }
    });

    it('should keep focus in the field when the popup is pressed', () => {
      const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
      options()[1].dispatchEvent(event);
      expect(event.defaultPrevented).toBe(true);
    });

    it('should reopen the popup when focus comes back from outside', async () => {
      const outside = fixture.nativeElement.querySelector('.outside');
      await focusout(outside);
      focusin(outside);
      expect(component.showAutocomplete()).toBe(true);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should not reopen when Escape moves focus from an option back to the input', () => {
      const option = options()[0];
      escape();
      focusin(option);
      expect(component.showAutocomplete()).toBe(false);
    });

    it('should not reopen on focus when there are no results', () => {
      component.onSearchClear();
      focusin(null);
      expect(component.showAutocomplete()).toBe(false);
      expect(component.showNoResults()).toBe(false);
    });

    it('should reopen on a click in the input after Escape', () => {
      escape();
      click(searchShadow().querySelector('input')!);
      expect(component.showAutocomplete()).toBe(true);
    });

    it('should not reopen on a click elsewhere in the field', () => {
      escape();
      click(searchShadow().querySelector('button')!);
      expect(component.showAutocomplete()).toBe(false);
    });
  });

  describe('keyboard navigation', () => {
    // Dispatch real bubbling events so the host: { '(keydown)': ... } binding is
    // actually exercised — calling onKeydown() directly would pass even if the
    // binding were deleted.
    const keydown = (key: string, target?: HTMLElement) => {
      const event = new KeyboardEvent('keydown', { key, cancelable: true, bubbles: true });
      (target ?? element).dispatchEvent(event);
      return event;
    };

    beforeEach(() => {
      component.results.set(items);
      component.showAutocomplete.set(true);
    });

    it('should handle arrow keys bubbling up from a focused option', () => {
      fixture.detectChanges();
      component.highlightedIndex.set(0);
      keydown('ArrowDown', options()[0]);
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should move highlight down on ArrowDown', () => {
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(0);
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should wrap to first item when pressing ArrowDown on the last item', () => {
      keydown('ArrowDown');
      keydown('ArrowDown');
      keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(0);
    });

    it('should wrap to last item when pressing ArrowUp with nothing highlighted', () => {
      keydown('ArrowUp');
      expect(component.highlightedIndex()).toBe(1);
    });

    it('should prevent default on arrow keys', () => {
      expect(keydown('ArrowDown').defaultPrevented).toBe(true);
      expect(keydown('ArrowUp').defaultPrevented).toBe(true);
    });

    it('should ignore arrow keys when there are no results', () => {
      component.showAutocomplete.set(false);
      component.results.set([]);
      const event = keydown('ArrowDown');
      expect(component.highlightedIndex()).toBe(-1);
      expect(component.showAutocomplete()).toBe(false);
      expect(event.defaultPrevented).toBe(false);
    });

    it('should select the highlighted item on submit', () => {
      keydown('ArrowDown');
      keydown('ArrowDown');
      component.onSearchSubmit();
      expect(host.selected).toEqual([items[1]]);
    });

    it('should fall back to the first result on submit when nothing is highlighted', () => {
      component.onSearchSubmit();
      expect(host.selected).toEqual([items[0]]);
    });

    it('should close the dropdown and reset the highlight on Escape', () => {
      keydown('ArrowDown');
      component.onSearchClear();
      component.results.set(items);
      component.showAutocomplete.set(true);
      keydown('ArrowDown');
      const event = keydown('Escape');
      expect(event.defaultPrevented).toBe(true);
      expect(component.showAutocomplete()).toBe(false);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should not select anything on submit after Escape dismissed the popup', () => {
      keydown('Escape');
      component.onSearchSubmit();
      expect(host.selected).toEqual([]);
    });

    it('should reopen the popup on ArrowDown after Escape dismissed it', () => {
      fixture.detectChanges();
      keydown('Escape');
      fixture.detectChanges();
      // The list must really be gone from the DOM — focus must survive that.
      expect(element.querySelector('.autocomplete-list')).toBeNull();
      keydown('ArrowDown');
      expect(component.showAutocomplete()).toBe(true);
      expect(component.highlightedIndex()).toBe(0);
      fixture.detectChanges();
      expect(activeElement()).toBe(options()[0]);
    });

    it('should keep the live region in the DOM even when the popup is closed', () => {
      component.showAutocomplete.set(false);
      component.showNoResults.set(false);
      fixture.detectChanges();
      expect(element.querySelector('[role="status"]')).toBeTruthy();
    });

    it('should announce the result count in the live region when open', () => {
      fixture.detectChanges();
      const liveRegion = element.querySelector('[role="status"]');
      expect(liveRegion).toBeTruthy();
      expect(liveRegion?.textContent?.trim().length).toBeGreaterThan(0);
    });

    it('should reset the highlight on new input', () => {
      keydown('ArrowDown');
      input('kjø');
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should reset the highlight after selecting', () => {
      keydown('ArrowDown');
      component.select(items[0]);
      expect(component.highlightedIndex()).toBe(-1);
    });

    it('should rove tabindex so only the highlighted option is tabbable', () => {
      fixture.detectChanges();
      keydown('ArrowDown');
      fixture.detectChanges();
      expect(options()[0].getAttribute('tabindex')).toBe('0');
      expect(options()[1].getAttribute('tabindex')).toBe('-1');
    });

    it('should move DOM focus to the highlighted option', () => {
      fixture.detectChanges();
      keydown('ArrowDown');
      fixture.detectChanges();
      expect(activeElement()).toBe(options()[0]);
    });

    it('should select the item on Enter keydown on an option', () => {
      fixture.detectChanges();
      options()[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      expect(host.selected).toEqual([items[1]]);
    });

    it('should select the item on Space keydown on an option', () => {
      fixture.detectChanges();
      const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });
      options()[1].dispatchEvent(event);
      expect(host.selected).toEqual([items[1]]);
      expect(event.defaultPrevented).toBe(true);
    });

    it('should give options ids that are unique to this instance', () => {
      fixture.detectChanges();
      const other = TestBed.createComponent(TestHostComponent);
      other.detectChanges();
      const otherComponent: AutocompleteComponent<Item> = other.debugElement.query(By.directive(AutocompleteComponent)).componentInstance;
      expect(options()[0].id).toBe(`${component.optionIdPrefix}0`);
      expect(otherComponent.optionIdPrefix).not.toBe(component.optionIdPrefix);
    });
  });

  it('should resize the popup when the field changes size', () => {
    // jsdom has no ResizeObserver; this one lets the test trigger it.
    let notifyResize = () => undefined;
    const view = window as unknown as { ResizeObserver: unknown };
    const original = view.ResizeObserver;
    view.ResizeObserver = class {
      constructor(callback: () => undefined) {
        notifyResize = callback;
      }
      observe = () => undefined;
      disconnect = () => undefined;
    };
    try {
      const resized = TestBed.createComponent(TestHostComponent);
      resized.detectChanges();
      const field = resized.debugElement.query(By.directive(AutocompleteComponent));
      const autocomplete: AutocompleteComponent<Item> = field.componentInstance;
      const box = (width: number) =>
        ({ top: 0, bottom: 40, left: 0, right: width, width, height: 40, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
      const fieldRect = vi.spyOn(field.nativeElement as HTMLElement, 'getBoundingClientRect').mockReturnValue(box(200));
      autocomplete.results.set(items);
      autocomplete.showAutocomplete.set(true);
      resized.detectChanges();
      const list = (field.nativeElement as HTMLElement).querySelector<HTMLElement>('.autocomplete-list')!;
      expect(list.style.width).toBe('200px');

      fieldRect.mockReturnValue(box(300));
      notifyResize();
      resized.detectChanges();

      expect(list.style.width).toBe('300px');
    } finally {
      view.ResizeObserver = original;
    }
  });

  describe('popup width', () => {
    const popup = () => element.querySelector<HTMLElement>('.autocomplete-list')!;
    const rect = (top: number, bottom: number, left = 20, width = 200) =>
      ({ top, bottom, left, right: left + width, width, height: bottom - top, x: left, y: top, toJSON: () => ({}) }) as DOMRect;

    beforeEach(() => component.results.set(items));

    it('should keep the popup inside the field and exactly as wide as it by default', () => {
      component.showAutocomplete.set(true);
      fixture.detectChanges();

      expect(popup().getAttribute('popover')).toBeNull();
      expect(popup().style.width).not.toBe('');
      expect(popup().style.minWidth).toBe('');
    });

    describe('with fitContent', () => {
      const showPopover = vi.fn();

      beforeEach(() => {
        // jsdom has no Popover API.
        Object.defineProperty(HTMLElement.prototype, 'popover', {
          configurable: true,
          get(this: HTMLElement) {
            return this.getAttribute('popover');
          },
        });
        HTMLElement.prototype.showPopover = showPopover;
        showPopover.mockClear();
        host.fitContent.set(true);
      });

      afterEach(() => {
        Reflect.deleteProperty(HTMLElement.prototype, 'popover');
        Reflect.deleteProperty(HTMLElement.prototype, 'showPopover');
      });

      it('should show the popup in the top layer, at least as wide as the field', () => {
        vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(rect(100, 140));
        component.showAutocomplete.set(true);
        fixture.detectChanges();

        expect(popup().getAttribute('popover')).toBe('manual');
        expect(showPopover).toHaveBeenCalledTimes(1);
        expect(popup().style.top).toBe('140px');
        expect(popup().style.left).toBe('20px');
        expect(popup().style.minWidth).toBe('200px');
        expect(popup().style.width).toBe('');
      });

      it('should follow the field when a parent scrolls', () => {
        const fieldRect = vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(rect(100, 140));
        component.showAutocomplete.set(true);
        fixture.detectChanges();

        fieldRect.mockReturnValue(rect(60, 100));
        fixture.nativeElement.querySelector('.scroller').dispatchEvent(new Event('scroll'));
        fixture.detectChanges();

        expect(popup().style.top).toBe('100px');
      });

      it('should hide the popup while the field is scrolled out of view', () => {
        vi.spyOn(fixture.nativeElement.querySelector('.scroller'), 'getBoundingClientRect').mockReturnValue(rect(0, 500));
        const fieldRect = vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(rect(100, 140));
        component.showAutocomplete.set(true);
        fixture.detectChanges();
        expect(popup().style.visibility).toBe('visible');

        fieldRect.mockReturnValue(rect(-60, -20));
        fixture.nativeElement.querySelector('.scroller').dispatchEvent(new Event('scroll'));
        fixture.detectChanges();

        expect(popup().style.visibility).toBe('hidden');
      });

      it('should use the top layer for the no-results message too', () => {
        component.showNoResults.set(true);
        fixture.detectChanges();

        expect(element.querySelector('.autocomplete-no-results')?.getAttribute('popover')).toBe('manual');
        expect(showPopover).toHaveBeenCalledTimes(1);
      });
    });
  });

  describe('no results', () => {
    it('should show the search term in the message', () => {
      const translate = TestBed.inject(TranslateService);
      translate.setTranslation('no', { autocomplete: { noResultsFor: 'Ingen treff på “{{searchTerm}}”.' } });
      translate.use('no');
      component.searchTerm.set('Coryfella');
      component.showNoResults.set(true);
      fixture.detectChanges();
      const message = element.querySelector('.autocomplete-no-results');
      expect(message?.textContent).toContain('Ingen treff på “Coryfella”.');
      expect(message?.querySelector('adb-icon[name="info"]')).toBeTruthy();
    });
  });
});
