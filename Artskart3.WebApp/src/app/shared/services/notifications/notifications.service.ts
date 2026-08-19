import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationModel } from '../../types/api.types';
import { AlertService, AlertVariant } from '../alert/alert.service';

// Assumed AlertType ordering (0-4) from the backend enum; confirm against the C# AlertType enum and adjust if it differs.
const ALERT_TYPE_VARIANT: Record<number, AlertVariant> = {
  0: 'info',
  1: 'success',
  2: 'warning',
  3: 'danger',
  4: 'danger',
};

@Injectable({
  providedIn: 'root',
})
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly alertService = inject(AlertService);
  private readonly endpoint = '/api/Notifications';

  getNotifications(): Observable<NotificationModel[]> {
    return this.http.get<NotificationModel[]>(this.endpoint);
  }

  /** Fetches notifications and shows the currently active ones as alerts. */
  loadAndShowNotifications(): void {
    this.getNotifications().subscribe((notifications) => {
      notifications.filter((n) => this.isActive(n)).forEach((n) => this.showNotification(n));
    });
  }

  private isActive(notification: NotificationModel): boolean {
    const now = Date.now();
    const start = notification.startDateTime ? new Date(notification.startDateTime).getTime() : -Infinity;
    const end = notification.endDateTime ? new Date(notification.endDateTime).getTime() : Infinity;
    return now >= start && now <= end;
  }

  private showNotification(notification: NotificationModel): void {
    const variant = ALERT_TYPE_VARIANT[notification.type ?? 0] ?? 'info';
    this.alertService.show(notification.description ?? '', variant, {
      heading: notification.heading ?? undefined,
      closable: notification.canClose ?? true,
      autoDismissMs: 0,
      startDisplayDate: notification.startDisplayDate ?? undefined,
      endDisplayDate: notification.endDisplayDate ?? undefined,
    });
  }
}