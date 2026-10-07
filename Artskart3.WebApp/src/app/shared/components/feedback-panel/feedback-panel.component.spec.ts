import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { FeedbackPanelComponent } from './feedback-panel.component';

describe('FeedbackPanelComponent', () => {
  let component: FeedbackPanelComponent;
  let fixture: ComponentFixture<FeedbackPanelComponent>;

  const el = (): HTMLElement => fixture.nativeElement;
  const tab = (): HTMLButtonElement => el().querySelector('.feedback-tab')!;
  const panel = (): HTMLElement => el().querySelector('.feedback-panel')!;
  const closeButton = (): HTMLButtonElement => el().querySelector('.feedback-close-btn')!;
  const pressEscape = () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeedbackPanelComponent],
      providers: [provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(FeedbackPanelComponent);
    component = fixture.componentInstance;
    document.body.appendChild(el());
    fixture.detectChanges();
  });

  afterEach(() => {
    el().remove();
  });

  it('should be closed by default', () => {
    expect(component.isOpen()).toBe(false);
    expect(tab().getAttribute('aria-expanded')).toBe('false');
    expect(panel().inert).toBe(true);
    expect(el().classList.contains('open')).toBe(false);
  });

  it('should link the tab to the panel', () => {
    expect(tab().getAttribute('aria-controls')).toBe(panel().id);
    expect(panel().getAttribute('aria-labelledby')).toBe(el().querySelector('h2')!.id);
  });

  it('should open when the tab is clicked', () => {
    tab().click();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(true);
    expect(tab().getAttribute('aria-expanded')).toBe('true');
    expect(panel().inert).toBe(false);
    expect(el().classList.contains('open')).toBe(true);
  });

  it('should move focus to the heading when opened', async () => {
    tab().click();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve));

    expect(document.activeElement).toBe(el().querySelector('h2'));
  });

  it('should close when the tab is clicked again', () => {
    tab().click();
    fixture.detectChanges();
    tab().click();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
    expect(panel().inert).toBe(true);
  });

  it('should close on Escape and return focus to the tab', () => {
    component.open();
    fixture.detectChanges();

    pressEscape();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
    expect(document.activeElement).toBe(tab());
  });

  it('should ignore Escape when closed', () => {
    const focusSpy = vi.spyOn(tab(), 'focus');

    pressEscape();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
    expect(focusSpy).not.toHaveBeenCalled();
  });

  it('should close when the close button is clicked and return focus to the tab', () => {
    component.open();
    fixture.detectChanges();

    closeButton().click();
    fixture.detectChanges();

    expect(component.isOpen()).toBe(false);
    expect(document.activeElement).toBe(tab());
  });

  it('should render the feedback links as external links', () => {
    const links = Array.from(el().querySelectorAll<HTMLAnchorElement>('.feedback-panel a'));

    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      'https://forms.cloud.microsoft/e/Ns51ygm4rB?origin=lprLink',
      'https://github.com/orgs/Artsdatabanken/projects/53',
    ]);
    for (const link of links) {
      expect(link.getAttribute('target')).toBe('_blank');
      expect(link.getAttribute('rel')).toBe('noopener noreferrer');
      expect(link.classList).toContain('adb-link--external');
    }
  });
});
