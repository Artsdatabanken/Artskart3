import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { NotificationModel } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class NotificationsService {
  private readonly notifications = httpResource<NotificationModel[]>(() => '/api/Notifications', {
    defaultValue: [],
  });

readonly activeNotifications = computed(() => {
  if (!this.notifications.hasValue()) {
    return [];
  }

  return this.notifications.value().filter(notification => this.isActive(notification));
});

  private isActive(notification: NotificationModel): boolean {
    const now = Date.now();

    const start = notification.startDisplayDate
      ? this.parseLocalDate(notification.startDisplayDate)?.getTime() ?? NaN
      : -Infinity;

    const end = notification.endDisplayDate
      ? this.parseLocalDate(notification.endDisplayDate, true)?.getTime() ?? NaN
      : Infinity;

    // Guard against invalid date strings (NaN)
    if (Number.isNaN(start) || Number.isNaN(end)) {
      return false;
    }

    return now >= start && now <= end;
  }

  private parseLocalDate(value: string, endOfDay = false): Date | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return null;

    const [, year, month, day] = match;
    const date = new Date(
      Number(year),
      Number(month) - 1,
      Number(day),
      endOfDay ? 23 : 0,
      endOfDay ? 59 : 0,
      endOfDay ? 59 : 0,
      endOfDay ? 999 : 0,
    );

    return date.getFullYear() === Number(year) &&
      date.getMonth() === Number(month) - 1 &&
      date.getDate() === Number(day)
      ? date
      : null;
  }
}