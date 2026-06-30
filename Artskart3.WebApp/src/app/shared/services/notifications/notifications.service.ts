import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { NotificationModel } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Notifications';

  getNotifications(): Observable<NotificationModel[]> {
    return this.http.get<NotificationModel[]>(this.endpoint);
  }
}