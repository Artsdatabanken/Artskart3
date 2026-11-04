import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { NotificationModel } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class NotificationsService {
  private readonly resource = httpResource<NotificationModel[]>(() => '/api/Notifications');

  readonly notifications = computed(() => (this.resource.hasValue() ? this.resource.value() : []));
}
