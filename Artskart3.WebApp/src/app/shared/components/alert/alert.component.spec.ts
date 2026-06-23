import { TestBed } from '@angular/core/testing';
import { AlertComponent } from './alert.component';
import { AlertService } from '../../services/alert/alert.service';

describe('AlertComponent', () => {
  let alertService: AlertService;

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [AlertComponent],
    }).compileComponents();

    alertService = TestBed.inject(AlertService);
    const fixture = TestBed.createComponent(AlertComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('should create', async () => {
    const fixture = await setup();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should not render adb-alert when there are no alerts', async () => {
    const fixture = await setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('adb-alert')).toBeNull();
  });

  it('should render adb-alert when alert is added', async () => {
    const fixture = await setup();
    alertService.showError('Something broke');
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const alerts = el.querySelectorAll('adb-alert');
    expect(alerts.length).toBe(1);
    expect(alerts[0].getAttribute('variant')).toBe('danger');
    expect(alerts[0].textContent?.trim()).toBe('Something broke');
  });

  it('should render multiple alerts', async () => {
    const fixture = await setup();
    alertService.showError('Error', { autoDismissMs: 0 });
    alertService.showSuccess('Done', { autoDismissMs: 0 });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const alerts = el.querySelectorAll('adb-alert');
    expect(alerts.length).toBe(2);
  });

  it('should render max 3 alerts even when more are queued', async () => {
    const fixture = await setup();
    alertService.show('1', 'info', { autoDismissMs: 0 });
    alertService.show('2', 'info', { autoDismissMs: 0 });
    alertService.show('3', 'info', { autoDismissMs: 0 });
    alertService.show('4', 'info', { autoDismissMs: 0 });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('adb-alert').length).toBe(3);
  });

  it('should set the heading attribute when heading is provided', async () => {
    const fixture = await setup();
    alertService.showWarning('Watch out', { heading: 'Heads up' });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const adbAlert = el.querySelector('adb-alert');
    expect(adbAlert!.getAttribute('heading')).toBe('Heads up');
  });

  it('should not set heading attribute when heading is not provided', async () => {
    const fixture = await setup();
    alertService.showInfo('Just so you know');
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const adbAlert = el.querySelector('adb-alert');
    expect(adbAlert!.getAttribute('heading')).toBeNull();
  });

  it('should set closable attribute when closable is true', async () => {
    const fixture = await setup();
    alertService.showSuccess('Done!', { closable: true });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const adbAlert = el.querySelector('adb-alert');
    expect(adbAlert!.hasAttribute('closable')).toBe(true);
  });

  it('should not set closable attribute when closable is false', async () => {
    const fixture = await setup();
    alertService.showError('Error', { closable: false });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const adbAlert = el.querySelector('adb-alert');
    expect(adbAlert!.hasAttribute('closable')).toBe(false);
  });

  it('should dismiss specific alert on adb-close event', async () => {
    const fixture = await setup();
    alertService.showError('First', { autoDismissMs: 0 });
    alertService.showWarning('Second', { autoDismissMs: 0 });
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const firstAlert = el.querySelector('adb-alert')!;
    firstAlert.dispatchEvent(new CustomEvent('adb-close', { bubbles: true }));
    fixture.detectChanges();

    const remaining = el.querySelectorAll('adb-alert');
    expect(remaining.length).toBe(1);
    expect(remaining[0].textContent?.trim()).toBe('Second');
  });

  it('should render the correct variant for each type', async () => {
    const fixture = await setup();

    const cases: [() => void, string][] = [
      [() => alertService.showError('e'), 'danger'],
      [() => alertService.showWarning('w'), 'warning'],
      [() => alertService.showSuccess('s'), 'success'],
      [() => alertService.showInfo('i'), 'info'],
    ];

    for (const [action, expectedVariant] of cases) {
      alertService.dismissAll();
      action();
      fixture.detectChanges();
      const adbAlert = (fixture.nativeElement as HTMLElement).querySelector('adb-alert');
      expect(adbAlert!.getAttribute('variant')).toBe(expectedVariant);
    }
  });
});
