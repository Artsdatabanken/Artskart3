import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { NotificationsComponent } from './notifications.component';
import { NotificationDismissalService } from '../../services/notifications/notification-dismissal.service';
import { NotificationModel } from '../../types/api.types';

const ID_ONE = '11111111-1111-1111-1111-111111111111';
const ID_TWO = '22222222-2222-2222-2222-222222222222';

function notification(overrides: Partial<NotificationModel>): NotificationModel {
  return { id: ID_ONE, alertType: 2, heading: 'Heading', description: 'Description', canClose: true, ...overrides };
}

describe('NotificationsComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    document.cookie = 'artskart.notifications.dismissed=; max-age=0; path=/';
    await TestBed.configureTestingModule({
      imports: [NotificationsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService()],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    document.cookie = 'artskart.notifications.dismissed=; max-age=0; path=/';
  });

  async function render(notifications: NotificationModel[]) {
    const fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();
    httpTesting.expectOne('/api/Notifications').flush(notifications);
    await fixture.whenStable();
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    return { fixture, element, alerts: () => Array.from(element.querySelectorAll('adb-alert')) };
  }

  it('renders nothing before the notifications have loaded', () => {
    const fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('adb-alert')).toHaveLength(0);
    httpTesting.expectOne('/api/Notifications').flush([]);
  });

  it('renders heading and description for each notification', async () => {
    const { alerts } = await render([
      notification({ id: ID_ONE, heading: 'First', description: 'First description' }),
      notification({ id: ID_TWO, heading: 'Second', description: 'Second description' }),
    ]);

    expect(alerts()).toHaveLength(2);
    expect(alerts()[0].getAttribute('heading')).toBe('First');
    expect(alerts()[0].textContent).toContain('First description');
    expect(alerts()[1].getAttribute('heading')).toBe('Second');
  });

  it.each([
    [0, 'danger'],
    [1, 'warning'],
    [2, 'info'],
    [3, 'success'],
    [4, 'info'],
  ])('maps alertType %i to variant %s', async (alertType, variant) => {
    const { alerts } = await render([notification({ alertType: alertType as NotificationModel['alertType'] })]);

    expect(alerts()[0].getAttribute('variant')).toBe(variant);
  });

  it('falls back to the danger variant when alertType is missing', async () => {
    const { alerts } = await render([notification({ alertType: undefined })]);

    expect(alerts()[0].getAttribute('variant')).toBe('danger');
  });

  it('makes the alert closable unless canClose is false', async () => {
    const { alerts } = await render([
      notification({ id: ID_ONE, canClose: true }),
      notification({ id: ID_TWO, canClose: false }),
    ]);

    expect(alerts()[0].hasAttribute('closable')).toBe(true);
    expect(alerts()[1].hasAttribute('closable')).toBe(false);
  });

  it('shows the date range only when a display date is set', async () => {
    const { alerts } = await render([
      notification({ id: ID_ONE, startDisplayDate: '2026-01-01', endDisplayDate: '2026-01-31' }),
      notification({ id: ID_TWO }),
    ]);

    expect(alerts()[0].querySelector('.alert-date-range')).not.toBeNull();
    expect(alerts()[1].querySelector('.alert-date-range')).toBeNull();
  });

  it('hides notifications that have already been dismissed', async () => {
    TestBed.inject(NotificationDismissalService).dismiss(notification({ id: ID_ONE }));

    const { alerts } = await render([
      notification({ id: ID_ONE, heading: 'Dismissed' }),
      notification({ id: ID_TWO, heading: 'Visible' }),
    ]);

    expect(alerts()).toHaveLength(1);
    expect(alerts()[0].getAttribute('heading')).toBe('Visible');
  });

  it('removes a notification when its alert is closed', async () => {
    const { fixture, alerts } = await render([
      notification({ id: ID_ONE, heading: 'First' }),
      notification({ id: ID_TWO, heading: 'Second' }),
    ]);

    alerts()[0].dispatchEvent(new CustomEvent('adb-close'));
    fixture.detectChanges();

    expect(alerts()).toHaveLength(1);
    expect(alerts()[0].getAttribute('heading')).toBe('Second');
  });
});
