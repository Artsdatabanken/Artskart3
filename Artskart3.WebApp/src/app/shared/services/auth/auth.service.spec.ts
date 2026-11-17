import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from './auth.service';

const STORAGE_KEY = 'artskart.anonymousUserId';

class MockBroadcastChannel {
  static readonly instances: MockBroadcastChannel[] = [];

  readonly postMessage = vi.fn();
  readonly addEventListener = vi.fn();
  readonly removeEventListener = vi.fn();
  readonly close = vi.fn();

  constructor(public readonly name: string) {
    MockBroadcastChannel.instances.push(this);
  }
}

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    MockBroadcastChannel.instances.length = 0;
    vi.stubGlobal('BroadcastChannel', MockBroadcastChannel);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpTesting = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.removeItem(STORAGE_KEY);
    vi.unstubAllGlobals();
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

  it('does not notify other tabs when only BFF claims change', () => {
    const channel = MockBroadcastChannel.instances[0];
    const session = [
      { type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' },
      { type: 'name', value: 'Test user' },
      { type: 'bff:session_expires_in', value: '300' },
    ];

    httpTesting.expectOne('bff/user').flush(session);
    channel.postMessage.mockClear();

    window.dispatchEvent(new Event('focus'));
    httpTesting.expectOne('bff/user').flush([{ ...session[0] }, { ...session[1] }, { type: 'bff:session_expires_in', value: '299' }]);

    expect(channel.postMessage).not.toHaveBeenCalled();
  });

  it('notifies other tabs when a non-BFF claim changes', () => {
    const channel = MockBroadcastChannel.instances[0];
    const session = [
      { type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' },
      { type: 'name', value: 'Test user' },
      { type: 'bff:session_expires_in', value: '300' },
    ];

    httpTesting.expectOne('bff/user').flush(session);
    channel.postMessage.mockClear();

    window.dispatchEvent(new Event('focus'));
    httpTesting.expectOne('bff/user').flush([{ ...session[0] }, { type: 'name', value: 'Updated user' }, { ...session[2] }]);

    expect(channel.postMessage).toHaveBeenCalledWith('session-changed');
  });

  it('keeps the same session value when only BFF claims change', () => {
    const session = [
      { type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' },
      { type: 'bff:session_expires_in', value: '300' },
    ];
    httpTesting.expectOne('bff/user').flush(session);
    const sessionBefore = service.session();

    window.dispatchEvent(new Event('focus'));
    httpTesting.expectOne('bff/user').flush([{ ...session[0] }, { type: 'bff:session_expires_in', value: '299' }]);

    expect(service.session()).toBe(sessionBefore);
  });

  it('keeps the session when the lookup fails for a reason other than 401', () => {
    const channel = MockBroadcastChannel.instances[0];
    httpTesting.expectOne('bff/user').flush([{ type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' }]);
    localStorage.setItem(STORAGE_KEY, 'anonymous-token');
    channel.postMessage.mockClear();

    window.dispatchEvent(new Event('focus'));
    httpTesting.expectOne('bff/user').error(new ProgressEvent('error'));

    expect(service.isAuthenticated()).toBe(true);
    expect(channel.postMessage).not.toHaveBeenCalled();
    expect(localStorage.getItem(STORAGE_KEY)).toBe('anonymous-token');
  });

  it('treats a 401 from the session lookup as logged out', () => {
    const channel = MockBroadcastChannel.instances[0];
    httpTesting.expectOne('bff/user').flush([{ type: 'sub', value: '25db4012-849e-4411-8616-f46b94a390dd' }]);

    window.dispatchEvent(new Event('focus'));
    httpTesting.expectOne('bff/user').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(service.isAnonymous()).toBe(true);
    expect(channel.postMessage).toHaveBeenCalledWith('logout');
  });

  it('clears the anonymous token when logging out', () => {
    httpTesting.expectOne('bff/user').flush(null);
    localStorage.setItem(STORAGE_KEY, 'anonymous-token');

    service.logout();

    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
