import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AlertService } from './alert.service';

describe('AlertService', () => {
  let service: AlertService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AlertService);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with no visible alerts', () => {
    expect(service.visibleAlerts()).toEqual([]);
  });

  describe('show()', () => {
    it('should add an alert with defaults', () => {
      service.show('Something went wrong', 'danger');
      const alerts = service.visibleAlerts();
      expect(alerts.length).toBe(1);
      expect(alerts[0].message).toBe('Something went wrong');
      expect(alerts[0].variant).toBe('danger');
      expect(alerts[0].closable).toBe(true);
      expect(alerts[0].autoDismissMs).toBe(5000);
      expect(alerts[0].heading).toBeUndefined();
    });

    it('should apply AlertOptions', () => {
      service.show('Heads up', 'warning', {
        heading: 'Warning',
        closable: false,
        autoDismissMs: 0,
      });
      const alert = service.visibleAlerts()[0];
      expect(alert.heading).toBe('Warning');
      expect(alert.closable).toBe(false);
      expect(alert.autoDismissMs).toBe(0);
    });

    it('should support multiple alerts', () => {
      service.show('First', 'info');
      service.show('Second', 'success');
      service.show('Third', 'warning');
      expect(service.visibleAlerts().length).toBe(3);
    });

    it('should show max 3 visible alerts and queue the rest', () => {
      service.show('1', 'info', { autoDismissMs: 0 });
      service.show('2', 'info', { autoDismissMs: 0 });
      service.show('3', 'info', { autoDismissMs: 0 });
      service.show('4', 'info', { autoDismissMs: 0 });
      expect(service.visibleAlerts().length).toBe(3);
      expect(service.visibleAlerts()[0].message).toBe('1');
      expect(service.visibleAlerts()[2].message).toBe('3');
    });

    it('should promote queued alert when a visible one is dismissed', () => {
      service.show('1', 'info', { autoDismissMs: 0 });
      service.show('2', 'info', { autoDismissMs: 0 });
      service.show('3', 'info', { autoDismissMs: 0 });
      service.show('4', 'info', { autoDismissMs: 0 });

      const firstId = service.visibleAlerts()[0].id;
      service.dismiss(firstId);

      expect(service.visibleAlerts().length).toBe(3);
      expect(service.visibleAlerts()[0].message).toBe('2');
      expect(service.visibleAlerts()[2].message).toBe('4');
    });
  });

  describe('variant helpers', () => {
    it('showError() sets danger variant', () => {
      service.showError('Error!');
      expect(service.visibleAlerts()[0].variant).toBe('danger');
    });

    it('showWarning() sets warning variant', () => {
      service.showWarning('Warning!');
      expect(service.visibleAlerts()[0].variant).toBe('warning');
    });

    it('showSuccess() sets success variant', () => {
      service.showSuccess('Done!');
      expect(service.visibleAlerts()[0].variant).toBe('success');
    });

    it('showInfo() sets info variant', () => {
      service.showInfo('FYI');
      expect(service.visibleAlerts()[0].variant).toBe('info');
    });
  });

  describe('dismiss()', () => {
    it('should remove the specified alert by id', () => {
      service.showError('Oops');
      const id = service.visibleAlerts()[0].id;
      service.dismiss(id);
      expect(service.visibleAlerts()).toEqual([]);
    });
  });

  describe('dismissAll()', () => {
    it('should clear all alerts', () => {
      service.show('A', 'info', { autoDismissMs: 0 });
      service.show('B', 'warning', { autoDismissMs: 0 });
      service.dismissAll();
      expect(service.visibleAlerts()).toEqual([]);
    });
  });

  describe('auto-dismiss', () => {
    it('should auto-dismiss after the default timeout', () => {
      vi.useFakeTimers();
      service.showSuccess('Saved!');
      expect(service.visibleAlerts().length).toBe(1);
      vi.advanceTimersByTime(5000);
      expect(service.visibleAlerts().length).toBe(0);
    });

    it('should NOT auto-dismiss when autoDismissMs is 0', () => {
      vi.useFakeTimers();
      service.showError('Persistent error', { autoDismissMs: 0 });
      vi.advanceTimersByTime(10000);
      expect(service.visibleAlerts().length).toBe(1);
    });

    it('should only dismiss the correct alert by timer', () => {
      vi.useFakeTimers();
      service.showSuccess('Short', { autoDismissMs: 2000 });
      service.showInfo('Long', { autoDismissMs: 10000 });
      vi.advanceTimersByTime(2000);
      expect(service.visibleAlerts().length).toBe(1);
      expect(service.visibleAlerts()[0].message).toBe('Long');
    });
  });
});
