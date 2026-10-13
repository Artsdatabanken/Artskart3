import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import '@artsdatabanken/components';
import { ObservationDetailsComponent } from './observation-details.component';

interface DesignSystemElement extends HTMLElement {
  readonly updateComplete: Promise<boolean>;
}

describe('ObservationDetails', () => {
  async function settle(fixture: ComponentFixture<ObservationDetailsComponent>): Promise<void> {
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const elements = host.querySelectorAll<DesignSystemElement>('adb-icon-button, adb-minimal-button, adb-accordion-item');
    await Promise.all([...elements].map((element) => element.updateComplete));
  }

  function headerButtons(fixture: ComponentFixture<ObservationDetailsComponent>): HTMLButtonElement[] {
    const element: HTMLElement = fixture.nativeElement;
    return [...element.querySelectorAll('.navigation adb-icon-button, .navigation adb-minimal-button')].map((host) =>
      host.shadowRoot!.querySelector('button')!,
    );
  }

  function accordionDetails(fixture: ComponentFixture<ObservationDetailsComponent>): HTMLDetailsElement[] {
    const element: HTMLElement = fixture.nativeElement;
    return [...element.querySelectorAll('adb-accordion-item')].map((host) => host.shadowRoot!.querySelector('details')!);
  }

  async function create() {
    await TestBed.configureTestingModule({ imports: [ObservationDetailsComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationDetailsComponent);
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('detail', { id: 42, scientificName: 'Vulpes vulpes', images: [] });
    fixture.componentRef.setInput('state', 'ready');
    await settle(fixture);
    return fixture;
  }

  it('starts with only the first section open without inventing validation, coordinates or photos', async () => {
    const fixture = await create();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelectorAll('adb-accordion')).toHaveLength(1);
    expect(element.querySelectorAll('[ngAccordionGroup], [ngAccordionTrigger]')).toHaveLength(0);
    expect(accordionDetails(fixture)).toHaveLength(3);
    expect(accordionDetails(fixture).map((details) => details.open)).toEqual([true, false, false]);
    expect(element.textContent).toContain('observationDetails.noCoordinates');
    expect(element.textContent).not.toContain('observationDetails.validated');
    expect(element.querySelector('app-observation-gallery')).toBeNull();
  });

  it('preserves section choices across observations and layout changes', async () => {
    const fixture = await create();
    const component = fixture.componentInstance;
    accordionDetails(fixture)[0].querySelector('summary')!.click();
    accordionDetails(fixture)[1].querySelector('summary')!.click();
    await vi.waitFor(() => expect(component.sectionsOpen()['observation']).toBe(false));
    await vi.waitFor(() => expect(component.sectionsOpen()['place']).toBe(true));
    expect(component.sectionsOpen()['dataset']).toBe(false);
    component.toggleExpanded();
    await settle(fixture);
    expect(fixture.nativeElement.querySelector('dialog').classList.contains('expanded')).toBe(true);
    fixture.componentRef.setInput('state', 'loading');
    await settle(fixture);
    fixture.componentRef.setInput('observationId', 43);
    fixture.componentRef.setInput('detail', { id: 43 });
    fixture.componentRef.setInput('state', 'ready');
    await settle(fixture);
    expect(component.sectionsOpen()['observation']).toBe(false);
    expect(accordionDetails(fixture).map((details) => details.open)).toEqual([false, true, false]);
    component.toggleExpanded();
    await settle(fixture);
    expect(component.sectionsOpen()['observation']).toBe(false);
  });

  it('disables navigation for standalone records and lets Escape close details', async () => {
    const fixture = await create();
    const dismiss = vi.fn();
    const previous = vi.fn();
    const next = vi.fn();
    fixture.componentInstance.dismiss.subscribe(dismiss);
    fixture.componentInstance.previous.subscribe(previous);
    fixture.componentInstance.next.subscribe(next);
    const buttons = headerButtons(fixture);
    expect(buttons).toHaveLength(5);
    expect(buttons[1].disabled).toBe(true);
    expect(buttons[2].disabled).toBe(true);
    buttons[1].click();
    buttons[2].click();
    expect(previous).not.toHaveBeenCalled();
    expect(next).not.toHaveBeenCalled();
    fixture.nativeElement.querySelector('dialog').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(dismiss).toHaveBeenCalledOnce();
  });

  it('shows mode-specific layout controls without dismissing details and transfers shadow focus', async () => {
    const fixture = await create();
    const component = fixture.componentInstance;
    fixture.componentRef.setInput('count', 2);
    await settle(fixture);
    const previous = vi.fn();
    const next = vi.fn();
    const dismiss = vi.fn();
    component.previous.subscribe(previous);
    component.next.subscribe(next);
    component.dismiss.subscribe(dismiss);
    const buttons = headerButtons(fixture);
    const element: HTMLElement = fixture.nativeElement;
    const back = element.querySelector<HTMLElement>('adb-minimal-button[aria-label="observationDetails.back"]')!;
    const expand = element.querySelector<HTMLElement>('adb-minimal-button[aria-label="observationDetails.expand"]')!;
    expect(back.hidden).toBe(true);
    expect(expand.hidden).toBe(false);
    expect(element.querySelectorAll('.navigation > button, .pager > button')).toHaveLength(0);
    for (const host of element.querySelectorAll('adb-icon-button, adb-minimal-button')) {
      expect(host.getAttribute('aria-label')).toBeTruthy();
      expect(host.querySelector('.visually-hidden')?.textContent).toBe(host.getAttribute('aria-label'));
    }
    buttons[1].click();
    buttons[2].click();
    expect(previous).toHaveBeenCalledOnce();
    expect(next).toHaveBeenCalledOnce();
    buttons[3].focus();
    buttons[3].click();
    await settle(fixture);
    expect(component.expanded()).toBe(true);
    expect(back.hidden).toBe(false);
    expect(expand.hidden).toBe(true);
    expect(back.shadowRoot!.activeElement).toBe(buttons[0]);
    expect(dismiss).not.toHaveBeenCalled();
    buttons[0].click();
    await settle(fixture);
    expect(component.expanded()).toBe(false);
    expect(back.hidden).toBe(true);
    expect(expand.hidden).toBe(false);
    expect(expand.shadowRoot!.activeElement).toBe(buttons[3]);
    expect(component.observationId()).toBe(42);
    expect(dismiss).not.toHaveBeenCalled();
    buttons[4].click();
    expect(dismiss).toHaveBeenCalledOnce();
    buttons[3].click();
    await settle(fixture);
    buttons[4].click();
    expect(dismiss).toHaveBeenCalledTimes(2);
  });

  it('replaces close with a back-to-list button in the narrow layout when there is a list', async () => {
    const fixture = await create();
    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;
    const dismiss = vi.fn();
    component.dismiss.subscribe(dismiss);
    const backToList = () => element.querySelector<HTMLElement>('adb-minimal-button[aria-label="observationDetails.backToList"]');
    const close = element.querySelector<HTMLElement>('adb-minimal-button[aria-label="common.close"]')!;
    expect(backToList()).toBeNull();
    expect(close.hidden).toBe(false);
    fixture.componentRef.setInput('hasList', true);
    await settle(fixture);
    expect(close.hidden).toBe(true);
    expect(backToList()!.querySelector('.visually-hidden')?.textContent).toBe('observationDetails.backToList');
    backToList()!.shadowRoot!.querySelector('button')!.click();
    expect(dismiss).toHaveBeenCalledOnce();
    component.toggleExpanded();
    await settle(fixture);
    expect(backToList()).toBeNull();
    expect(close.hidden).toBe(false);
  });

  it('marks missing values as empty with field-specific text and reports no known quality issues', async () => {
    const fixture = await create();
    const element: HTMLElement = fixture.nativeElement;
    const value = (label: string) => [...element.querySelectorAll('dt')].find((dt) => dt.textContent === label + ':')!.nextElementSibling!;
    expect(value('observationDetails.fields.collector').textContent).toBe('observationDetails.notProvided');
    expect(value('observationDetails.fields.collector').classList).toContain('empty');
    expect(value('observationDetails.fields.institution').textContent).toBe('observationDetails.noInstitution');
    expect(value('observationDetails.fields.projects').textContent).toBe('observationDetails.noProjects');
    expect(value('observationDetails.fields.dataset').textContent).toBe('observationDetails.noDataset');
    expect(value('observationDetails.fields.quality').textContent).toBe('observationDetails.noKnownIssues');
    expect(value('observationDetails.fields.quality').classList).not.toContain('empty');
  });

  it('never hides a quality warning behind a validated label', async () => {
    const fixture = await create();
    fixture.componentRef.setInput('detail', { id: 42, quality: 2, tags: ['Validated'], hasErrors: true });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('observationDetails.validated');
    expect(fixture.nativeElement.textContent).toContain('observationDetails.qualities.2');
    expect(fixture.nativeElement.textContent).toContain('observationDetails.qualityWarning');
  });
});
