import { TestBed } from '@angular/core/testing';
import { NotificationDismissalService } from './notification-dismissal.service';
import { NotificationModel } from '../../types/api.types';

interface CookieInformationTestWindow {
  CookieInformation?: {
    getConsentGivenFor(category: string): boolean;
  };
}

const notification: NotificationModel = {
  heading: 'Planned maintenance',
  startDisplayDate: '2026-01-01',
  endDisplayDate: '2026-01-31',
};

function setConsent(given: boolean): void {
  (window as unknown as CookieInformationTestWindow).CookieInformation = {
    getConsentGivenFor: () => given,
  };
}

function clearCookies(): void {
  document.cookie = 'artskart.notifications.dismissed=; max-age=0; path=/';
}

describe('NotificationDismissalService', () => {
  let service: NotificationDismissalService;

  beforeEach(() => {
    clearCookies();
    delete (window as unknown as CookieInformationTestWindow).CookieInformation;
    TestBed.configureTestingModule({});
    service = TestBed.inject(NotificationDismissalService);
  });

  afterEach(() => {
    clearCookies();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should not be dismissed initially', () => {
    expect(service.isDismissed(notification)).toBe(false);
  });

  it('should treat a notification as dismissed for the current session regardless of consent', () => {
    service.dismiss(notification);
    expect(service.isDismissed(notification)).toBe(true);
  });

  it('should not persist a cookie without consent for necessary cookies', () => {
    setConsent(false);
    service.dismiss(notification);
    expect(document.cookie).not.toContain('artskart.notifications.dismissed');
  });

  it('should persist a cookie when consent for necessary cookies is given', () => {
    setConsent(true);
    service.dismiss(notification);
    expect(document.cookie).toContain('artskart.notifications.dismissed');
  });

  it('should restore dismissed notifications from the cookie on a new instance', () => {
    setConsent(true);
    service.dismiss(notification);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const newInstance = TestBed.inject(NotificationDismissalService);

    expect(newInstance.isDismissed(notification)).toBe(true);
  });

  it('should not treat a different notification as dismissed', () => {
    setConsent(true);
    service.dismiss(notification);

    const other: NotificationModel = { ...notification, heading: 'Different notice' };
    expect(service.isDismissed(other)).toBe(false);
  });
});
