import { ApplicationRef } from '@angular/core';
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

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call GET /api/Notifications and expose the notifications', async () => {
    const mockResponse: NotificationModel[] = [
      {
        id: '11111111-1111-1111-1111-111111111111',
        alertType: 1,
        heading: 'Planlagt vedlikehold',
        description: 'Fredag kl. 22',
        canClose: true,
      },
    ];

    TestBed.tick();
    const req = httpTesting.expectOne('/api/Notifications');
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
    await settle();

    expect(service.notifications()).toEqual(mockResponse);
  });

  it('should be empty before the request has completed', () => {
    TestBed.tick();
    httpTesting.expectOne('/api/Notifications');

    expect(service.notifications()).toEqual([]);
  });

  it('should return an empty list when there are no notifications', async () => {
    TestBed.tick();
    httpTesting.expectOne('/api/Notifications').flush([]);
    await settle();

    expect(service.notifications()).toEqual([]);
  });

  it('should fall back to an empty list on http errors', async () => {
    TestBed.tick();
    httpTesting.expectOne('/api/Notifications').flush('error', { status: 500, statusText: 'Server Error' });
    await settle();

    expect(service.notifications()).toEqual([]);
  });

  it('should only fetch once for multiple reads', async () => {
    TestBed.tick();
    httpTesting.expectOne('/api/Notifications').flush([]);
    await settle();

    service.notifications();
    service.notifications();

    httpTesting.expectNone('/api/Notifications');
  });
});
