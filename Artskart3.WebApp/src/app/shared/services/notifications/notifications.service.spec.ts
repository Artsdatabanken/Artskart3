import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { NotificationsService } from './notifications.service';
import { NotificationModel } from '../../types/api.types';

describe('NotificationsService', () => {
  let service: NotificationsService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(NotificationsService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call GET /api/Notifications and return the notifications', () => {
    const mockResponse: NotificationModel[] = [
      {
        id: '11111111-1111-1111-1111-111111111111',
        alertType: 1,
        heading: 'Planlagt vedlikehold',
        description: 'Fredag kl. 22',
        canClose: true,
      },
    ];
    let result: NotificationModel[] | undefined;

    service.getNotifications().subscribe((notifications) => (result = notifications));

    const req = httpTesting.expectOne('/api/Notifications');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
    expect(result).toEqual(mockResponse);
  });

  it('should return an empty list when there are no notifications', () => {
    let result: NotificationModel[] | undefined;

    service.getNotifications().subscribe((notifications) => (result = notifications));

    httpTesting.expectOne('/api/Notifications').flush([]);
    expect(result).toEqual([]);
  });

  it('should propagate http errors to the subscriber', () => {
    let status: number | undefined;

    service.getNotifications().subscribe({ error: (error) => (status = error.status) });

    httpTesting.expectOne('/api/Notifications').flush('error', { status: 500, statusText: 'Server Error' });
    expect(status).toBe(500);
  });
});
