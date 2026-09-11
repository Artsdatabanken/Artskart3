import '@artsdatabanken/components';
import { ChangeDetectionStrategy, Component, CUSTOM_ELEMENTS_SCHEMA, computed, inject, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DateRangePipe } from '../../pipes/date-range.pipe';
import { AlertVariant } from '../../services/alert/alert.service';
import { NotificationsService } from '../../services/notifications/notifications.service';
import { NotificationModel } from '../../types/api.types';

const ALERT_TYPE_VARIANT: Record<number, AlertVariant> = {
  0: 'danger',
  1: 'warning',
  2: 'info',
  3: 'success',
  4: 'info',
};

@Component({
  selector: 'app-notifications',
  imports: [DateRangePipe, TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css',
})
export class NotificationsComponent {
  protected readonly notificationsService = inject(NotificationsService);
  protected readonly translate = inject(TranslateService);
  private readonly dismissedNotifications = signal<ReadonlySet<NotificationModel>>(new Set());
  protected readonly visibleNotifications = computed(() =>
    this.notificationsService.activeNotifications().filter(notification => !this.dismissedNotifications().has(notification))
  );

  protected variant(notification: NotificationModel): AlertVariant {
    return ALERT_TYPE_VARIANT[notification.type ?? 0] ?? 'info';
  }

  protected dismiss(notification: NotificationModel): void {
    this.dismissedNotifications.update(dismissed => new Set(dismissed).add(notification));
  }
}