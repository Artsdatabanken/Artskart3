import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

const STORAGE_KEY = 'artskart.anonymousUserId';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpTesting = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(STORAGE_KEY);
  });

  it('does not report an anonymous session until the initial lookup resolves', () => {
    expect(service.sessionResolved()).toBe(false);

    httpTesting.expectOne('bff/user').flush(null);

    expect(service.sessionResolved()).toBe(true);
    expect(service.isAnonymous()).toBe(true);
  });

  it('refreshes the session when the tab regains focus', () => {
    httpTesting.expectOne('bff/user').flush(null);

    window.dispatchEvent(new Event('focus'));

    httpTesting.expectOne('bff/user').flush([
      { type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' },
      { type: 'name', value: 'Test user' },
    ]);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.sessionResolved()).toBe(true);
  });

  it('clears the anonymous token when logging out', () => {
    httpTesting.expectOne('bff/user').flush(null);
    localStorage.setItem(STORAGE_KEY, 'anonymous-token');

    service.logout();

    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
