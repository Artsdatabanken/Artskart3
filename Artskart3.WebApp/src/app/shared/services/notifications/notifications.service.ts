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

  readonly activeNotifications = computed(() => this.notifications.value().filter(notification => this.isActive(notification)));

  private isActive(notification: NotificationModel): boolean {
    const now = Date.now();
    const start = notification.startDateTime ? new Date(notification.startDateTime).getTime() : -Infinity;
    const end = notification.endDateTime ? new Date(notification.endDateTime).getTime() : Infinity;
    return now >= start && now <= end;
  }
}