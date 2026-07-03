import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ModalComponent } from './modal.component';

@Component({
  imports: [ModalComponent, TranslateModule],
  template: `
    <app-modal
      [open]="open()"
      [title]="title()"
      [variant]="variant()"
      [confirmLabel]="confirmLabel()"
      [cancelLabel]="cancelLabel()"
      [confirmDisabled]="confirmDisabled()"
      (modalConfirm)="onConfirm()"
      (modalCancel)="onCancel()"
    >
      <p>Test body content</p>
    </app-modal>
  `,
})
class TestHostComponent {
  open = signal(false);
  title = signal('Test Title');
  variant = signal<'prompt' | 'info'>('info');
  confirmLabel = signal('OK');
  cancelLabel = signal('Cancel');
  confirmDisabled = signal(false);
  confirmed = false;
  cancelled = false;

  onConfirm() {
    this.confirmed = true;
  }
  onCancel() {
    this.cancelled = true;
  }
}

describe('ModalComponent', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let host: TestHostComponent;

  const setup = async (variant: 'info' | 'prompt' = 'info', open = false) => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent, TranslateModule.forRoot()],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    host = fixture.componentInstance;
    host.variant.set(variant);
    host.open.set(open);
    fixture.detectChanges();
    return fixture;
  };

  const el = (): HTMLElement => fixture.nativeElement;

  it('should not render when closed', async () => {
    await setup('info', false);
    expect(el().querySelector('.modal-backdrop')).toBeNull();
  });

  it('should render backdrop and dialog when open', async () => {
    await setup('info', true);
    expect(el().querySelector('.modal-backdrop')).toBeTruthy();
    expect(el().querySelector('[role="dialog"]')).toBeTruthy();
  });

  it('should have aria-modal attribute', async () => {
    await setup('info', true);
    const dialog = el().querySelector('[role="dialog"]')!;
    expect(dialog.getAttribute('aria-modal')).toBe('true');
  });

  it('should have aria-labelledby pointing to modal title', async () => {
    await setup('info', true);
    const dialog = el().querySelector('[role="dialog"]')!;
    const labelledBy = dialog.getAttribute('aria-labelledby')!;
    expect(labelledBy).toMatch(/^modal-title-\d+$/);
    const title = el().querySelector(`#${labelledBy}`);
    expect(title).toBeTruthy();
    expect(title!.textContent).toBe('Test Title');
  });

  it('should display projected content', async () => {
    await setup('info', true);
    expect(el().textContent).toContain('Test body content');
  });

  it('should show close X button for info variant', async () => {
    await setup('info', true);
    expect(el().querySelector('.modal-close-btn')).toBeTruthy();
  });

  it('should not show close X button for prompt variant', async () => {
    await setup('prompt', true);
    expect(el().querySelector('.modal-close-btn')).toBeNull();
  });

  it('should show cancel button for prompt variant', async () => {
    await setup('prompt', true);
    const buttons = el().querySelectorAll('adb-button');
    expect(buttons.length).toBe(2);
  });

  it('should show only confirm button for info variant', async () => {
    await setup('info', true);
    // info has a confirm button + the X close button
    const adbButtons = el().querySelectorAll('adb-button');
    expect(adbButtons.length).toBe(1);
  });

  it('should emit confirm on confirm button click', async () => {
    await setup('info', true);
    const confirmBtn = el().querySelector('.modal-footer adb-button') as HTMLElement;
    confirmBtn.click();
    expect(host.confirmed).toBe(true);
  });

  it('should emit cancel when X button clicked (info variant)', async () => {
    await setup('info', true);
    const closeBtn = el().querySelector('.modal-close-btn') as HTMLElement;
    closeBtn.click();
    expect(host.cancelled).toBe(true);
  });

  it('should emit cancel on Escape key', async () => {
    await setup('info', true);
    const backdrop = el().querySelector('.modal-backdrop') as HTMLElement;
    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(host.cancelled).toBe(true);
  });

  it('should emit cancel when clicking backdrop', async () => {
    await setup('info', true);
    const backdrop = el().querySelector('.modal-backdrop') as HTMLElement;
    backdrop.click();
    expect(host.cancelled).toBe(true);
  });

  it('should not emit cancel when clicking inside dialog', async () => {
    await setup('info', true);
    const dialog = el().querySelector('.modal-dialog') as HTMLElement;
    dialog.click();
    expect(host.cancelled).toBe(false);
  });

  it('should set disabled attribute on confirm button when confirmDisabled is true', async () => {
    await setup('prompt', true);
    host.confirmDisabled.set(true);
    fixture.detectChanges();
    const buttons = el().querySelectorAll('adb-button');
    const confirmBtn = buttons[buttons.length - 1];
    expect(confirmBtn.hasAttribute('disabled')).toBe(true);
  });

  it('should render with custom labels', async () => {
    await setup('prompt', true);
    host.confirmLabel.set('Submit');
    host.cancelLabel.set('Abort');
    fixture.detectChanges();
    const buttonsText = Array.from(el().querySelectorAll('adb-button')).map((b) =>
      b.textContent?.trim(),
    );
    expect(buttonsText).toContain('Submit');
    expect(buttonsText).toContain('Abort');
  });
});
